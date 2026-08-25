using Avr8Sharp.TestKit.Boards;
using FluentAssertions;
using NUnit.Framework;

namespace PyMCU.IntegrationTests.Tests.AVR;

/// <summary>
/// Integration tests for fixtures/signed-mixed-width (PyMCU#92, PyMCU#94).
///
/// Three answers CPython gives in one line each: a negative product divided back down, a
/// signed value compared against a larger unsigned one, and the same signed value against a
/// literal that does not fit its type. All three were wrong, and none of them stops the build.
/// </summary>
[TestFixture]
public class SignedMixedWidthTests
{
    private static SimSession _session = null!;

    [OneTimeSetUp]
    public void BuildFirmware() => _session = new SimSession(PymcuCompiler.BuildFixture("signed-mixed-width"));

    private ArduinoUnoSimulation Boot()
    {
        var uno = _session.Reset();
        uno.RunUntilSerial(uno.Serial, "done\n", maxMs: 800);
        return uno;
    }

    [Test]
    public void TheNegativeProductDividesBackDown()
    {
        Boot().Serial.Text.Should().StartWith("-7\n", "-7 * 7 // 7 is -7, and it printed 9355");
    }

    [Test]
    public void ASignedValueComparesAsItsOwnSign()
    {
        Boot().Serial.Text.Should().Contain("x<=y\n", "int8(100) > uint8(200) is False");
    }

    [Test]
    public void ALiteralWiderThanTheTypeStillCompares()
    {
        Boot().Serial.Text.Should().Contain("x<200\n", "int8(100) < 200 is True");
    }
}
