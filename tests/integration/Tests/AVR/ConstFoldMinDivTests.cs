using Avr8Sharp.TestKit.Boards;
using FluentAssertions;
using NUnit.Framework;

namespace PyMCU.IntegrationTests.Tests.AVR;

/// <summary>
/// Integration tests for fixtures/const-fold-min-div (PyMCU#223).
///
/// Folding int32 MIN // -1 or MIN % -1 threw a .NET OverflowException that reached the user as
/// an InternalCompilerError at line 1:1. The fixture prints every value twice, folded from
/// literals and computed at run time from the same numbers made opaque, so the two columns can
/// be compared against each other rather than against a rule written down somewhere.
///
/// That is what makes this worth a firmware test: the compiler unit tests can pin what the
/// folder produces, but only running it can say whether the folder and the chip agree.
/// </summary>
[TestFixture]
public class ConstFoldMinDivTests
{
    private static SimSession _session = null!;

    [OneTimeSetUp]
    public void BuildFirmware() => _session = new SimSession(PymcuCompiler.BuildFixture("const-fold-min-div"));

    private string Boot()
    {
        var uno = _session.Reset();
        uno.RunUntilSerial(uno.Serial, "done\n", maxMs: 4000);
        return uno.Serial.Text;
    }

    [Test]
    public void TheInt32FloorAgreesWithTheChip()
    {
        Boot().Should().Contain("-2147483648\n-2147483648\n",
            "the folded value and the executed one, in that order; folding used to abort the build");
    }

    [Test]
    public void TheInt32ModuloAgreesWithTheChip()
    {
        Boot().Should().Contain("-2147483648\n-2147483648\n0\n0\ndone\n");
    }

    [Test]
    public void TheNarrowerWidthsWereAlreadyRight()
    {
        Boot().Should().StartWith("-128\n-128\n0\n0\n-32768\n-32768\n0\n0\n",
            "int8 and int16 always folded to their own wrap, which is why int32 wraps too");
    }

    [Test]
    public void NoColumnDisagreesWithTheOther()
    {
        var lines = Boot().Replace("\r", "").Split('\n');
        for (var i = 0; i + 1 < lines.Length && lines[i] != "done"; i += 2)
            lines[i + 1].Should().Be(lines[i],
                $"the folded value on line {i + 1} and the run-time value under it must match");
    }
}
