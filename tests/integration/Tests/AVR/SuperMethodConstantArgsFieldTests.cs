using Avr8Sharp.TestKit.Boards;
using FluentAssertions;
using NUnit.Framework;

namespace PyMCU.IntegrationTests.Tests.AVR;

/// <summary>
/// Integration test for fixtures/super-method-constant-args-field -- PyMCU#430.
/// super().describe() + self.extra must fold to 7 for Sub(3, 4).describe(); before the fix it
/// computed 28 by way of a NoneVal super() return, and after only half the fix (the return
/// value alone) it computed 4 from a "value"-named field that was never actually stored.
/// </summary>
[TestFixture]
public class SuperMethodConstantArgsFieldTests
{
    private static SimSession _session = null!;

    [OneTimeSetUp]
    public void BuildFirmware() =>
        _session = new SimSession(PymcuCompiler.BuildFixture("super-method-constant-args-field"));

    [Test]
    public void SuperCallPlusSubclassFieldComputesCorrectlyWithConstantConstructorArgs()
    {
        var uno = _session.Reset();
        uno.RunUntilSerial(uno.Serial, s => s.Contains("7"), maxMs: 400);
        uno.Serial.Text.Should().Contain("7",
            "Sub(3, 4).describe() must fold to super().describe() (3) + self.extra (4) = 7");
    }
}
