using System.Globalization;
using Avr8Sharp.TestKit.Boards;

var sim = new ArduinoUnoSimulation();
sim.WithHex(Console.In.ReadToEnd());
var maxMs = double.Parse(args[0], CultureInfo.InvariantCulture);
var timed = false;
var dumpStart = -1;
var dumpLen = 0;
foreach (var a in args.Skip(1))
{
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
