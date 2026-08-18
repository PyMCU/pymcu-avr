// SPDX-License-Identifier: MIT
using FluentAssertions;
using NUnit.Framework;
using Avr8Sharp.TestKit.Boards;
using Avr8Sharp.TestKit;

namespace PyMCU.IntegrationTests.Tests.AVR;

/// <summary>
/// Float comparisons go through the soft-float runtime, in every relation. The
/// integer ordering of IEEE754 bits puts negatives above positives, so a CP/CPC
/// chain answers backwards — and the registers it compared against were the ones
/// the arithmetic routine had just used as scratch. Each line reads pos, neg,
/// zero; sub/subp compare a real subtraction's result against zero.
/// </summary>
[TestFixture]
public class FloatCompareTests
{
    private SimSession _session = null!;

    [OneTimeSetUp]
    public void BuildFirmware() => _session = new SimSession(PymcuCompiler.BuildFixture("float-compare"));

    [Test]
    public void GreaterAndLess()
    {
        var uno = Sim();
        uno.RunUntilSerial(uno.Serial, "sub=0 subp=1\n", maxMs: 3000);
        uno.Serial.Should().ContainLine("gt=100");
        uno.Serial.Should().ContainLine("lt=010");
    }

    [Test]
    public void GreaterEqualAndLessEqual()
    {
        var uno = Sim();
        uno.RunUntilSerial(uno.Serial, "sub=0 subp=1\n", maxMs: 3000);
        uno.Serial.Should().ContainLine("ge=101");
        uno.Serial.Should().ContainLine("le=011");
    }

    [Test]
    public void EqualAndNotEqual()
    {
        var uno = Sim();
        uno.RunUntilSerial(uno.Serial, "sub=0 subp=1\n", maxMs: 3000);
        uno.Serial.Should().ContainLine("eq=001");
        uno.Serial.Should().ContainLine("ne=110");
    }

    [Test]
    public void SubtractionResultComparedAgainstZero()
    {
        var uno = Sim();
        uno.RunUntilSerial(uno.Serial, "sub=0 subp=1\n", maxMs: 3000);
        // 0.04 - 0.05 is negative, 0.05 - 0.04 positive — the shape that made
        // compat-cp-alarm sleep for a wrong duration.
        uno.Serial.Should().ContainLine("sub=0 subp=1");
    }

    private ArduinoUnoSimulation Sim() => _session.Reset();
}
