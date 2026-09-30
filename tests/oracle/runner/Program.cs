using System.Globalization;
using AVR8Sharp.Core.Peripherals;
using Avr8Sharp.TestKit.Boards;

var sim = new ArduinoUnoSimulation();
// The bus peripherals are attached only when a flag asks for them, so a program run
// without one sees the same simulation the language probes always had.
//   --adc=ch:volts   attach the ADC (every channel at 0 V) and set one channel; repeatable
//   --spi            attach SPI; each byte shifted out is logged and answered with its complement
//   --twi[=AA]       attach TWI with one device at hex address AA (default 3C). It ACKs its
//                    address and every byte written, answers reads with 0x10, 0x11, ...,
//                    and logs every bus event, repeated STARTs apart from STARTs
// SPI and TWI traffic goes to stderr, one event per line.
var busLog = new System.Text.StringBuilder();
AvrAdc? adc = null;
foreach (var a in args.Skip(1))
{
    if (a.StartsWith("--adc=", StringComparison.Ordinal) && adc is null)
    {
        sim.AddAdc(AvrAdc.AdcConfig, out adc);
        for (var i = 0; i < adc.ChannelValues.Length; i++) adc.ChannelValues[i] = 0;
    }
    else if (a == "--spi")
    {
        sim.AddSpi(AvrSpi.SpiConfig, out var spi);
        spi.OnTransfer = b => { busLog.Append($"SPI {b:X2}\n"); return (byte)(b ^ 0xFF); };
    }
    else if (a == "--twi" || a.StartsWith("--twi=", StringComparison.Ordinal))
    {
        var address = a == "--twi" ? (byte)0x3C : Convert.ToByte(a["--twi=".Length..], 16);
        sim.AddTwi(AvrTwi.TwiConfig, out var twi);
        twi.EventHandler = new LoggingTwiDevice(twi, address, busLog);
    }
}
sim.WithHex(Console.In.ReadToEnd());
// RFC 0013 (docs/rfcs/0013-memory-model.md, PyMCU-rfc13), section 7.2, ON BY
// DEFAULT since phase 0c ("static by exclusion", team-lead directive
// 2026-09-30): the same poisoning the integration suite's SimSession/
// UnoTwiTrace now default to (see tests/testkit/ColdBootRealism.cs, not
// referenced from this standalone runner, so inlined here identically: fill
// R0-R31 except R1 and SRAM 0x100-0x8FF with 0xFF -- or a different byte
// named by PYMCU_FORCE_POISON_COLD_BOOT -- before the first instruction).
// Measured before flipping this default: the full oracle corpus, both front
// ends, poisoned and unpoisoned, gave the identical pass/skip/xfail counts
// (415/14/11 and 412/14/14) -- 0 divergences from CPython either way.
var poisonEnv = Environment.GetEnvironmentVariable("PYMCU_FORCE_POISON_COLD_BOOT");
byte poisonByte = poisonEnv != null && byte.TryParse(poisonEnv, out var forcedPoison)
    ? forcedPoison : (byte)0xFF;
for (var r = 0; r <= 0x1F; r++)
    if (r != 1) sim.Data[r] = poisonByte;
for (var addr = 0x100; addr <= 0x8FF; addr++)
    sim.Data[addr] = poisonByte;
var maxMs = double.Parse(args[0], CultureInfo.InvariantCulture);
var timed = false;
var dumpStart = -1;
var dumpLen = 0;
foreach (var a in args.Skip(1))
{
    if (a.StartsWith("--adc=", StringComparison.Ordinal))
    {
        var ap = a["--adc=".Length..].Split(':');
        adc!.ChannelValues[int.Parse(ap[0], CultureInfo.InvariantCulture)] =
            double.Parse(ap[1], CultureInfo.InvariantCulture);
        continue;
    }
    if (a == "--timed")
    {
        timed = true;
        continue;
    }
    if (a.StartsWith("--dump=", StringComparison.Ordinal))
    {
        // --dump=start:len -- hex-dump SRAM [start, start+len) to stderr at exit.
        var dp = a["--dump=".Length..].Split(':');
        dumpStart = Convert.ToInt32(dp[0], 16); dumpLen = Convert.ToInt32(dp[1], 16);
        continue;
    }
    if (!a.StartsWith("--wire=", StringComparison.Ordinal)) continue;
    // A jumper between two pins: writes to the source pin drive the destination pin's
    // input latch, so a program that bit-bangs a frame on one pin is received on the
    // other -- the way test03_self_loopback_nec jumpers D4 to D3 on a real board.
    var pins = a["--wire=".Length..].Split(':');
    byte src = byte.Parse(pins[0][2..]), dst = byte.Parse(pins[1][2..]);
    sim.PortD.AddListener((nv, ov) =>
    {
        if (((nv ^ ov) & (1 << src)) != 0)
            sim.PortD.SetPinValue(dst, (nv & (1 << src)) != 0);
    });
}
try
{
    if (timed)
    {
        // Programs that poll forever (read_pulses' blocking loop) never print END; run the
        // budget and report whatever the UART accumulated.
        sim.RunMilliseconds(maxMs);
    }
    else
    {
        try
        {
            sim.RunUntilSerial(sim.Serial, "END\n", maxMs);
        }
        catch (TimeoutException) when (sim.Serial.Text.Length > 0)
        {
            // Preserve partial output so the oracle reports the first differing line.
        }
    }
}
finally
{
    // A crashed simulation still shows what the program printed before it died --
    // without this the partial output vanished with the exception.
    Console.Write(sim.Serial.Text.Replace("\r\n", "\n"));
    Console.Error.Write(busLog.ToString());
    if (dumpStart >= 0)
    {
        var sb = new System.Text.StringBuilder();
        for (var i = 0; i < dumpLen; i += 16)
        {
            sb.Append($"{dumpStart + i:X4}:");
            for (var j = 0; j < 16 && i + j < dumpLen; j++)
                sb.Append($" {sim.Data[dumpStart + i + j]:X2}");
            sb.Append('\n');
        }
        Console.Error.Write(sb.ToString());
    }
}

/// <summary>One I2C device at a fixed address that logs the bus as it sees it.</summary>
sealed class LoggingTwiDevice(AvrTwi twi, byte address, System.Text.StringBuilder log) : ITwiEventHandler
{
    private byte _next = 0x10;

    public void Start(bool repeated)
    {
        log.Append(repeated ? "TWI RSTART\n" : "TWI START\n");
        twi.CompleteStart();
    }

    public void Stop()
    {
        log.Append("TWI STOP\n");
        twi.CompleteStop();
    }

    public void ConnectToSlave(byte addr, bool write)
    {
        log.Append($"TWI ADDR {addr:X2} {(write ? "W" : "R")}\n");
        twi.CompleteConnect(addr == address);
    }

    public void WriteByte(byte data)
    {
        log.Append($"TWI W {data:X2}\n");
        twi.CompleteWrite(true);
    }

    public void ReadByte(bool ack)
    {
        log.Append($"TWI R {_next:X2} {(ack ? "ACK" : "NACK")}\n");
        twi.CompleteRead(_next++);
    }
}
