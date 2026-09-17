using Avr8Sharp.TestKit.Boards;
using FluentAssertions;
using NUnit.Framework;

namespace PyMCU.IntegrationTests.Tests.AVR;

/// <summary>
/// Integration tests for fixtures/inherited-property.
///
/// The adafruit_character_lcd constructor shape: `for pin in (a, b)` unrolls a
/// tuple of INSTANCES so the loop variable aliases each object, and
/// `pin.direction = v` expands the property setter through that alias. The
/// `message` setter is inherited from the base class -- the property tables
/// are keyed by the DEFINING class, so the lookup walks the MRO -- and its
/// `str` parameter carries the literal's text so `for c in m` unrolls inside
/// the setter body.
/// </summary>
[TestFixture]
public class InheritedPropertyTests
{
    private SimSession _session = null!;

    [OneTimeSetUp]
    public void BuildFirmware() => _session = new SimSession(PymcuCompiler.BuildFixture("inherited-property"));

    private string Transcript()
    {
        var uno = _session.Reset();
        uno.RunUntilSerial(uno.Serial, "END\n", maxMs: 800);
        return uno.Serial.Text;
    }

    [Test]
    public void TupleOfInstances_UnrollsAndSetterApplies()
        => Transcript().Should().Contain("A 1\nB 1\n",
            "the unrolled loop aliases each instance and the direction setter writes its field");

    [Test]
    public void InheritedSetter_RunsOnTheSubclassInstance()
        => Transcript().Should().Contain("C 3\n",
            "message.setter is defined on Base but assigns through a Sub instance");

    [Test]
    public void StrSetterParam_IteratesItsText()
        => Transcript().Should().Contain("D 1\n",
            "the second assignment rebinds the str parameter and unrolls one char");
}
