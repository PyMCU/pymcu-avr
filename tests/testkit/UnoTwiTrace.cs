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
    public static WireTrace Record(string hex, byte address, int stopAfterCount,
        double maxMs = 2000, double tailMs = 50)
    {
        var uno = new ArduinoUnoSimulation();
        uno.WithHex(hex);
        uno.AddTwi(AvrTwi.TwiConfig, out var twi);
        var recorder = new TwiRecorder(twi, address);
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
