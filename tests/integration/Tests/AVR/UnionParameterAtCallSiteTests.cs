using Avr8Sharp.TestKit;
using FluentAssertions;
using NUnit.Framework;

namespace PyMCU.IntegrationTests.Tests.AVR;

/// <summary>
/// fixtures/union-parameter-at-call-site (PyMCU#442): `Union[A, B]` on a parameter of an
/// @inline-expanded function or method, every constructor included.
///
/// A real subroutine has one ABI for every caller and nowhere to resolve which member a
/// given call means, and keeps its refusal. A constructor builds at its OWN call site, where
/// the argument's actual type is known -- exactly the way an @inline overload already
/// dispatches on an argument's type -- so the refusal there was answering a question the
/// call site had already answered. Found reducing three Adafruit libraries, all on a
/// constructor parameter: adafruit_character_lcd (two classes), adafruit_ht16k33 (int vs a
/// fixed array), adafruit_debouncer (a class vs a plain function reference).
/// </summary>
[TestFixture]
public class UnionParameterAtCallSiteTests
{
    private static SimSession _session = null!;

    [OneTimeSetUp]
    public void BuildFirmware()
    {
        _session = new SimSession(PymcuCompiler.BuildFixture("union-parameter-at-call-site"));
    }

    private static string Output()
    {
        var uno = _session.Reset();
        uno.RunMilliseconds(400);
        return uno.Serial.Text;
    }

    [Test]
    public void TheFirstClassMemberBindsTheFieldToThatClass()
        => Output().Should().Contain("holder_a 5");

    [Test]
    public void TheSecondClassMemberBindsTheFieldToThatClass()
        => Output().Should().Contain("holder_b 7");

    [Test]
    public void TheScalarMemberAcceptsTheDefaultArgument()
        => Output().Should().Contain("matrix_int 112", "0x70");

    [Test]
    public void TheListMemberAcceptsAFixedArrayLiteral()
        => Output().Should().Contain("matrix_list 1");

    [Test]
    public void AClassOrACallableBothBindThroughTheSameUnionParameter()
        => Output().Should().Contain("debounce_marker 2", "both constructions ran, one per member");

    [Test]
    public void TheProgramRunsToItsEnd() => Output().Should().Contain("done");
}
