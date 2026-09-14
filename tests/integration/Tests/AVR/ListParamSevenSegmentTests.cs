using FluentAssertions;
using NUnit.Framework;
using Avr8Sharp.TestKit.Boards;

namespace PyMCU.IntegrationTests.Tests.AVR;

/// <summary>
/// The acceptance program for PyMCU#313 and PyMCU#317, written the way a CircuitPython user
/// writes a 7-segment driver: the seven pins are built as a list of DigitalInOut and handed
/// to the class, and the digit patterns are a module-level list of ints with no annotation,
/// indexed by a digit only known at run time.
///
/// It was refused twice over. First at `for s in self._segments:`, because a list given to a
/// class was not a sequence; then at `pattern = DIGITS[digit]`, because a lookup table
/// written as a plain list has no storage to index and the reader was told to add an
/// annotation the idiom does not have.
///
/// Every assertion reads a PORT register. Reading the pin back would fold to the value just
/// written and pass on a firmware that drives nothing.
/// </summary>
[TestFixture]
public class ListParamSevenSegmentTests
{
    private SimSession _session = null!;

    [OneTimeSetUp]
    public void BuildFirmware() =>
        _session = new SimSession(PymcuCompiler.BuildFixture("list-param-seven-segment"));

    private string Transcript()
    {
        var uno = _session.Reset();
        uno.RunUntilSerial(uno.Serial, "END\n", maxMs: 2000);
        return uno.Serial.Text;
    }

    // PORTD & 0xFC is segments a..f (PD2..PD7), PORTB & 0x01 is segment g (PB0). The numbers
    // are the digit table in the fixture header, computed by CPython from DIGITS.
    [TestCase(0, 252, 0)]
    [TestCase(1, 24, 0)]
    [TestCase(2, 108, 1)]
    [TestCase(3, 60, 1)]
    [TestCase(4, 152, 1)]
    [TestCase(5, 180, 1)]
    [TestCase(6, 244, 1)]
    [TestCase(7, 28, 0)]
    [TestCase(8, 252, 1)]
    [TestCase(9, 188, 1)]
    public void EachDigitDrivesItsSegments(int digit, int portD, int portB)
        => Transcript().Split('\n')[digit].Trim()
            .Should().Be($"{portD} {portB}", $"digit {digit}");

    [Test]
    public void ClearTurnsEverySegmentOff()
        => Transcript().Should().Contain("0 0\nEND\n");
}
