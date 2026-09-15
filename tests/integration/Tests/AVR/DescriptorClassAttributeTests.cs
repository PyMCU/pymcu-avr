using Avr8Sharp.TestKit.Boards;
using FluentAssertions;
using NUnit.Framework;

namespace PyMCU.IntegrationTests.Tests.AVR;

/// <summary>
/// Integration tests for fixtures/descriptor-class-attribute.
///
/// PyMCU#268: a class-level attribute was reachable as `Cls.ATTR` and reported as
/// "'Dev' has no attribute 'LIMIT'" as `inst.ATTR`, on a program CPython runs.
///
/// PyMCU#360: a class attribute whose class defines `__get__` is a descriptor, so `inst.attr`
/// is `type(inst).attr.__get__(inst, type(inst))` and `inst.attr = v` is `__set__`. Only the
/// explicit spelling compiled, and nobody writes it.
///
/// Before the fix this fixture did not build at all. The assertions are on the values the
/// program prints, because a fixture that only checked it builds would pass with a descriptor
/// that returned the descriptor object.
///
/// Bases are exercised deliberately: class attributes do not inherit on their own, so
/// `Sub.LIMIT` declared on `Dev` is only found by walking to `Dev`.
/// </summary>
[TestFixture]
public class DescriptorClassAttributeTests
{
    private static SimSession _session = null!;

    [OneTimeSetUp]
    public void BuildFirmware() =>
        _session = new SimSession(PymcuCompiler.BuildFixture("descriptor-class-attribute"));

    private ArduinoUnoSimulation Boot()
    {
        var uno = _session.Reset();
        uno.RunUntilSerial(uno.Serial, "done\n", maxMs: 1000);
        return uno;
    }

    [Test]
    public void AClassConstant_IsReadableThroughTheInstance()
    {
        Boot().Serial.Text.Should().StartWith("7\n", "d.LIMIT is 7, as Dev.LIMIT always was");
    }

    [Test]
    public void ADescriptorRead_CallsGet()
    {
        var text = Boot().Serial.Text;
        text.Should().Contain("16\n", "d.flag is 0x12 & 0x10");
        text.Should().Contain("4\n", "d.other is 0x34 & 0x04");
    }

    [Test]
    public void ABaseClassAttribute_IsFoundThroughTheSubclass()
    {
        Boot().Serial.Text.Should().Contain("7\n16\n2\n",
            "s.LIMIT and s.flag are Dev's, found by walking to the base");
    }

    [Test]
    public void ADescriptorWrite_CallsSet()
    {
        var text = Boot().Serial.Text;
        text.Should().Contain("2\n", "d.flag = 0 clears bit 4 of buf[1], leaving 0x02");
        text.Should().Contain("52\n", "d.other = 1 sets a bit already set, leaving 0x34");
    }
}
