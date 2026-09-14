using Avr8Sharp.TestKit;
using Avr8Sharp.TestKit.Boards;
using FluentAssertions;
using NUnit.Framework;

namespace PyMCU.IntegrationTests.Tests.AVR;

/// <summary>
/// fixtures/raise-in-main (PyMCU#339): an unhandled `raise` written in the entry function
/// prints its name and halts.
///
/// It used to lower to the propagate-to-caller form, `SET; RET`, and main has no caller: the
/// stack pointer is at the top of SRAM, so the RET popped a return address that was never
/// pushed and execution went wherever those bytes point. avr8sharp reported a stack underflow
/// at that instruction and nothing reached the UART -- where the documented behaviour is
/// `E:&lt;TypeName&gt;` then a halt, never a silent continue.
///
/// The seed comes from GPIOR0, written into the simulation before the run: qemu does not
/// retain a write to that register, so a program that seeds itself reads back zero and one
/// branch is picked for the reader.
/// </summary>
[TestFixture]
public class RaiseInMainTests
{
    private static SimSession _session = null!;

    [OneTimeSetUp]
    public void BuildFirmware() => _session = new SimSession(PymcuCompiler.BuildFixture("raise-in-main"));

    private static ArduinoUnoSimulation RunWithSeed(byte seed)
    {
        var uno = _session.Reset();
        uno.Data[0x3E] = seed;              // GPIOR0
        uno.RunMilliseconds(200);
        return uno;
    }

    [Test]
    public void TheRaisingSeed_PrintsTheExceptionName()
    {
        RunWithSeed(90).Serial.Text.Should().Contain("E:ValueError",
            "an exception nothing handles names itself on the UART before stopping");
    }

    [Test]
    public void TheRaisingSeed_NeverReachesTheLineAfterTheRaise()
    {
        RunWithSeed(90).Serial.Text.Should().NotContain("ALIVE",
            "the raise is unhandled, so nothing after it runs");
    }

    [Test]
    public void TheRaisingSeed_LeavesTheChipStopped()
    {
        var uno = RunWithSeed(90);
        var pc = uno.Cpu.Pc;
        uno.RunMilliseconds(50);
        uno.Cpu.Pc.Should().Be(pc,
            "the halt is a tight loop with interrupts off, not a return into whatever the "
            + "top of SRAM happened to hold");
    }

    [Test]
    public void AnyOtherSeed_RunsOnAsBefore()
    {
        var text = RunWithSeed(1).Serial.Text;
        text.Should().Contain("ALIVE");
        text.Should().NotContain("E:ValueError");
    }
}
