using FluentAssertions;
using NUnit.Framework;
using Avr8Sharp.TestKit.Boards;

namespace PyMCU.IntegrationTests.Tests.AVR;

/// <summary>
/// A MicroPython library compiled UNMODIFIED: `sevenseg.py` from
/// github.com/kritishmohapatra/micropython-sevenseg, vendored byte-identical under the
/// fixture's src/. That is the whole point of it, so the file must not be edited to make a
/// test pass; if the compiler regresses, this goes red and the library stays as its author
/// wrote it.
///
/// One file exercises six things that were each refused on their own: a comprehension of
/// instances over a list of pin numbers (PyMCU#332), a constant subscript of that list
/// (PyMCU#333), an optional peripheral guarded by a field set to None (PyMCU#334), a dict
/// literal in a field (PyMCU#335) whose values are lists (PyMCU#336), and zip over the field
/// of pins against a row of that dict (PyMCU#337).
///
/// Every assertion reads a PORT register. Reading the pin back would fold to the value just
/// written and pass on a firmware that drives nothing.
/// </summary>
[TestFixture]
public class ListParamMpSevenSegUpstreamTests
{
    private SimSession _session = null!;

    [OneTimeSetUp]
    public void BuildFirmware() =>
        _session = new SimSession(PymcuCompiler.BuildFixture("list-param-mp-sevenseg-upstream"));

    private string Transcript()
    {
        var uno = _session.Reset();
        uno.RunUntilSerial(uno.Serial, "END\n", maxMs: 3000);
        return uno.Serial.Text;
    }

    // PORTD & 0xFC is segments a..f (PD2..PD7), PORTB & 0x01 is segment g (PB0), common
    // cathode. The numbers are the library's own DIGITS table, computed by CPython.
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
            .Should().Be($"{portD} {portB}", $"digit {digit} of the library's own table");

    [Test]
    public void ClearTurnsEverySegmentOff()
        => Transcript().Should().Contain("0 0\nEND\n");
}
