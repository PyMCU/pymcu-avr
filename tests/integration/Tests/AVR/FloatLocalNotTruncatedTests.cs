using FluentAssertions;
using NUnit.Framework;

namespace PyMCU.IntegrationTests.Tests.AVR;

/// <summary>
/// Integration tests for fixtures/float-local-not-truncated (PyMCU#216).
///
/// `f = 1.5` with no annotation was typed uint8, so the store truncated and print wrote 1.
/// The inference chain in EmitScalarVarAssign had a case for a temporary, a variable, an
/// integer literal and a cast, and none for a float constant, so a float literal fell past
/// every branch and kept the UINT8 default.
///
/// Three discriminators and three controls. The controls are what place the defect rather than
/// decorate it: the annotated form and float arithmetic were always right, so the bug is in
/// inference and not in floats; and an explicit uint8() cast must STILL truncate -- a fix that
/// made every float survive would have broken the one case where truncating is what was asked
/// for.
///
/// Measured: the baseline firmware writes 1, 3 and 2 for the three discriminators.
/// </summary>
[TestFixture]
public class FloatLocalNotTruncatedTests
{
    private static SimSession _session = null!;

    [OneTimeSetUp]
    public void BuildFirmware()
        => _session = new SimSession(PymcuCompiler.BuildFixture("float-local-not-truncated"));

    private static string Output()
    {
        var uno = _session.Reset();
        uno.RunUntilSerial(uno.Serial, "done\n", maxMs: 2000);
        return uno.Serial.Text.Replace("\r\n", "\n");
    }

    // DISCRIMINATOR. The issue's own two-line program.
    [Test]
    public void ABareFloatLiteralKeepsItsFractionalPart()
        => Output().Should().StartWith("1.5\n", "f = 1.5 is a float, not the integer 1");

    // DISCRIMINATOR. A different value, so a fix that happened to produce 1.5 for one input
    // does not pass.
    [Test]
    public void ASecondBareFloatKeepsItsFractionalPartToo()
        => Output().Should().Contain("\n3.75\n", "3.75 truncated to 3 is the same defect");

    // DISCRIMINATOR. Rebinding the name to another float literal must not fall back either.
    [Test]
    public void AFloatReboundToAnotherLiteralIsStillAFloat()
        => Output().Should().Contain("\n2.5\n");

    // CONTROL. The annotated form and float arithmetic were always right, which is what says
    // the defect is in inference rather than in floats.
    [Test]
    public void TheAnnotatedFormAndFloatArithmeticStillWork()
        => Output().Should().Contain("\n4.0\n");

    // CONTROL. An integer local must stay an integer.
    [Test]
    public void AnIntegerLocalIsUnaffected()
        => Output().Should().Contain("\n7\n");

    // CONTROL, and the one a careless fix would break: asking for truncation still truncates.
    [Test]
    public void AnExplicitCastStillTruncates()
        => Output().Should().EndWith("\n1\ndone\n", "uint8(1.5) is a request to truncate");

    // The failure mode itself, stated separately: the three discriminators would all pass on a
    // build that printed the right floats and some stray integers as well.
    [Test]
    public void NoFloatIsPrintedAsItsTruncatedInteger()
    {
        var text = Output();

        text.Should().NotContain("\n1\n3\n", "1 and 3 in sequence is the truncated output");
        text.Should().NotStartWith("1\n", "the first line is 1.5, not 1");
    }
}
