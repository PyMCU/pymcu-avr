using Avr8Sharp.TestKit.Boards;
using FluentAssertions;
using NUnit.Framework;

namespace PyMCU.IntegrationTests.Tests.AVR;

/// <summary>
/// Integration test for fixtures/timer16-store-order (PyMCU#483, PyMCU#492).
///
/// The 16-bit timer registers share one TEMP latch: the LOW byte's write commits the pair
/// and takes the high half from TEMP, so a 16-bit store must emit the HIGH byte first.
/// Both store paths got it backwards -- the constant one was split low-then-high in the IR
/// generator, the runtime one lowered to STS low, STS high in the AVR code generator -- so
/// every 16-bit register write committed a stale TEMP as its high byte. The reads were
/// already right and must stay that way.
///
/// The fixture writes and reads back on the chip, so a swapped pair, a one-byte write and a
/// one-byte read all come out as wrong numbers instead of as missing output.
/// </summary>
[TestFixture]
public class Timer16StoreOrderTests
{
    private static SimSession _session = null!;

    [OneTimeSetUp]
    public void BuildFirmware() => _session = new SimSession(PymcuCompiler.BuildFixture("timer16-store-order"));

    [Test]
    public void SixteenBitRegisterWritesReadBackWholeThroughTheTempLatch()
    {
        var uno = _session.Reset();
        uno.RunUntilSerial(uno.Serial, "done\n", maxMs: 800);
        uno.Serial.Text.Should().Be("4660\n22136\n4660\ndone\n",
            "0x1234 into TCNT1 and 0x5678 into OCR1A (constant stores) and 0x1234 into " +
            "ICR1 (a runtime store) each have to reach the pair whole; a low-byte-first " +
            "store commits the high half from a stale TEMP instead");
    }
}
