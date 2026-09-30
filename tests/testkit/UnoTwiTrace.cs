using Avr8Sharp.TestKit.Boards;
using Avr8Sharp.TestKit;
using AVR8Sharp.Core.Peripherals;

namespace PyMCU.TestKit;

/// <summary>
/// Runs a pymcu-built Intel HEX image on the emulated Arduino Uno with a TWI
/// listener attached, and returns every transaction addressed to
/// <paramref name="address"/> until <paramref name="stopAfterCount"/> of them
/// have been seen (then a short tail, to catch anything emitted beyond).
/// </summary>
public static class UnoTwiTrace
{
    /// <param name="readScript">
    /// Bytes the slave answers read transactions with, in wire order; the tail
    /// of a read past the script's end is 0xFF. Null means the all-0xFF answer
    /// the recorder has always given.
    /// </param>
    public static WireTrace Record(string hex, byte address, int stopAfterCount,
        double maxMs = 2000, double tailMs = 50, byte[]? readScript = null,
        byte? poisonColdBoot = null)
        => Record(hex, twi =>
        {
            var r = new TwiRecorder(twi, address);
            if (readScript != null)
                foreach (var b in readScript) r.ReadBytes.Enqueue(b);
            return r;
        }, stopAfterCount, maxMs, tailMs, poisonColdBoot);

    /// <summary>
    /// Same run with a caller-supplied bus device -- a register file for a
    /// sensor whose driver validates chip id / calibration before streaming.
    /// </summary>
    /// <param name="poisonColdBoot">
    /// When set, fills R0-R31 (except R1) and SRAM with this byte before the
    /// firmware's first instruction -- see <see cref="ColdBootRealism"/>.
    /// Null (the default) leaves avr8sharp's own zeroed state alone.
    /// </param>
    public static WireTrace Record(string hex, Func<AvrTwi, ITwiRecorder> makeDevice,
        int stopAfterCount, double maxMs = 2000, double tailMs = 50,
        byte? poisonColdBoot = null)
    {
        var uno = new ArduinoUnoSimulation();
        uno.WithHex(hex);
        if (poisonColdBoot is byte poison) ColdBootRealism.Poison(uno, poison);
        // The GPIO model drives PIN only from injected values -- the internal
        // pull-ups do not raise it -- so hold SDA/SCL high the way a wired bus
        // sits at idle, or CircuitPython's busio.I2C wiring check refuses it.
        uno.PortC.SetPinValue(4, true);   // SDA
        uno.PortC.SetPinValue(5, true);   // SCL
        uno.AddTwi(AvrTwi.TwiConfig, out var twi);
        var recorder = makeDevice(twi);
        twi.EventHandler = recorder;

        string? crash = null;
        try
        {
            uno.RunUntilMs(_ => recorder.Transactions.Count >= stopAfterCount, maxMs: maxMs);
            uno.RunMilliseconds(tailMs);
        }
        catch (Exception ex)
        {
            // A firmware crash (stack underflow RET, PC out of flash, data access
            // past SRAM) or a timeout is itself the divergence: record it with
            // enough context to name the failing construct, and let the
            // transaction comparison produce the failure message.
            crash =
                $"simulation stopped abnormally: {ex.GetType().Name}: {ex.Message} " +
                $"at PC=0x{uno.Cpu.Pc:X4} (byte 0x{uno.Cpu.Pc * 2:X5}), " +
                $"SP=0x{uno.Cpu.Sp:X4}, SREG=0x{uno.Cpu.Sreg:X2}, " +
                $"cycles={uno.Cpu.Cycles}; {recorder.Transactions.Count} " +
                $"TWI transactions recorded before the stop";
            recorder.Flush();
        }
        return new WireTrace(recorder.Transactions, crash);
    }
}
