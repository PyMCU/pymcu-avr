using Avr8Sharp.TestKit;
using Avr8Sharp.TestKit.Boards;
using FluentAssertions;
using NUnit.Framework;

namespace PyMCU.IntegrationTests.Tests.AVR;

/// <summary>
/// fixtures/raise-no-print (PyMCU#340): an unhandled `raise` names itself on the wire even when
/// the program never prints.
///
/// The unhandled path writes `E:&lt;TypeName&gt;` to UART0 and halts, but UART0 was only ever set up
/// by the driver, and only when it saw `print(` or an explicit `UART(` in the sources. A program
/// that raises and never prints therefore wrote into a transmitter with TXEN0 clear: the bytes
/// went nowhere and the board simply stopped. That is the "silent continue" the limitations page
/// promises never happens, in another form -- a silent STOP, with nothing to read.
///
/// Nothing in the fixture touches the UART. Every byte this test reads was put on the wire by
/// the unhandled path, which now turns the transmitter on itself before writing.
///
/// The seed comes from GPIOR0, written into the simulation before the run: qemu does not retain
/// a write to that register, so a program that seeds itself reads back zero and one branch is
/// picked for the reader.
/// </summary>
[TestFixture]
public class RaiseNoPrintTests
{
    private const ushort Gpior1Addr = 0x4A;

    private static SimSession _session = null!;

    [OneTimeSetUp]
    public void BuildFirmware() => _session = new SimSession(PymcuCompiler.BuildFixture("raise-no-print"));

    private static ArduinoUnoSimulation RunWithSeed(byte seed)
    {
        var uno = _session.Reset();
        uno.Data[0x3E] = seed;              // GPIOR0
        uno.RunMilliseconds(200);
        return uno;
    }

    [Test]
    public void TheRaisingSeed_PrintsTheExceptionNameWithNoPrintInTheProgram()
    {
        RunWithSeed(90).Serial.Text.Should().Contain("E:ValueError",
            "the unhandled path enables the transmitter itself when nothing else in the "
            + "program has, so the message reaches the wire rather than a UART that is off");
    }

    [Test]
    public void TheRaisingSeed_LeavesTheChipStopped()
    {
        var uno = RunWithSeed(90);
        var pc = uno.Cpu.Pc;
        uno.RunMilliseconds(50);
        uno.Cpu.Pc.Should().Be(pc,
            "the halt is a tight loop with interrupts off, and turning the transmitter on "
            + "did not change what happens after the message");
    }

    [Test]
    public void AnyOtherSeed_RunsOnAndSaysNothing()
    {
        var uno = RunWithSeed(1);
        uno.Data[Gpior1Addr].Should().Be(0x55, "the non-raising branch runs to its marker");
        uno.Serial.Text.Should().NotContain("E:ValueError");
    }
}
