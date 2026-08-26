using FluentAssertions;
using NUnit.Framework;

namespace PyMCU.IntegrationTests.Tests.AVR;

/// <summary>
/// Integration tests for fixtures/onechar-string-equality (PyMCU#211).
///
/// `x = "a"` then `if x == "a":` folded FALSE and the branch that should have run was deleted
/// from the image. One string arrived as two numbers: a one-character literal in expression
/// position is its character code (97), and the same literal read back through a name is an
/// interned id (256), so the compile-time comparison compared two encodings of "a".
///
/// Two discriminators and four controls. Both directions of the operator are discriminators
/// because `!=` failed in mirror image and would pass a test that only checked `==`.
///
/// Measured: the baseline image contains `eq1-BROKEN` and does not contain `eq1` at all.
/// </summary>
[TestFixture]
public class OneCharStringEqualityTests
{
    private static SimSession _session = null!;

    [OneTimeSetUp]
    public void BuildFirmware()
        => _session = new SimSession(PymcuCompiler.BuildFixture("onechar-string-equality"));

    private static string Output()
    {
        var uno = _session.Reset();
        uno.RunUntilSerial(uno.Serial, "num\n", maxMs: 800);
        return uno.Serial.Text.Replace("\r\n", "\n");
    }

    // DISCRIMINATOR. Folded false before the fix, and the `eq1` branch was not in the image.
    [Test]
    public void AOneCharacterStringEqualsItself()
        => Output().Should().Contain("eq1\n", "\"a\" == \"a\" is true whichever path each side arrived by");

    // DISCRIMINATOR. The mirror image: `!=` folded true and printed the broken branch.
    [Test]
    public void AOneCharacterStringIsNotUnequalToItself()
    {
        var text = Output();

        text.Should().Contain("ne1\n", "\"a\" != \"a\" is false");
        text.Should().NotContain("eq1-BROKEN", "the broken branch is the one the old compiler emitted");
    }

    // CONTROL. Two characters always worked, because both sides took the interning path. It is
    // the matched pair that shows the defect was about length, not about equality.
    [Test]
    public void AMultiCharacterStringStillEqualsItself()
        => Output().Should().Contain("eq2\n").And.Contain("ne2\n");

    // CONTROL. A comparison that is genuinely false must stay false.
    [Test]
    public void TwoDifferentOneCharacterStringsAreStillUnequal()
        => Output().Should().Contain("diff\n").And.NotContain("diff-BROKEN");

    // CONTROL, and the one that matters most. A one-character literal is still its character
    // code where a number is what is meant -- which is what `uart.write('\n')` depends on, and
    // what removing the special case outright costs 192 integration tests over.
    [Test]
    public void AOneCharacterLiteralIsStillItsCharacterCode()
        => Output().Should().Contain("num\n").And.NotContain("num-BROKEN");
}
