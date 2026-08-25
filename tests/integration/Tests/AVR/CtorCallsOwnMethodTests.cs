using Avr8Sharp.TestKit.Boards;
using FluentAssertions;
using NUnit.Framework;

namespace PyMCU.IntegrationTests.Tests.AVR;

/// <summary>
/// Integration tests for fixtures/ctor-calls-own-method (PyMCU#93).
///
/// A constructor calling one of its own methods left the field the method assigned reading
/// zero, and the lost store landed on an unrelated module-level variable. Both halves are
/// checked here, along with the field the constructor set directly, so a fix that rescues the
/// write but scrambles the rest cannot pass.
/// </summary>
[TestFixture]
public class CtorCallsOwnMethodTests
{
    private static SimSession _session = null!;

    [OneTimeSetUp]
    public void BuildFirmware() => _session = new SimSession(PymcuCompiler.BuildFixture("ctor-calls-own-method"));

    [Test]
    public void TheMethodTheConstructorCalledKeepsItsWrite()
    {
        var uno = _session.Reset();
        uno.RunUntilSerial(uno.Serial, "done\n", maxMs: 800);
        uno.Serial.Text.Should().Contain("77\n", "self.calc() sets b to 77 during construction");
    }

    [Test]
    public void NothingElseIsOverwritten()
    {
        var uno = _session.Reset();
        uno.RunUntilSerial(uno.Serial, "done\n", maxMs: 800);
        uno.Serial.Text.Should().Contain("77\n111\n7\ndone\n", "the module-level guard and the directly-set field are untouched");
    }
}
