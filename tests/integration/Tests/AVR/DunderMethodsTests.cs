// SPDX-License-Identifier: MIT
using FluentAssertions;
using NUnit.Framework;
using Avr8Sharp.TestKit.Boards;
using Avr8Sharp.TestKit;

namespace PyMCU.IntegrationTests.Tests.AVR;

/// <summary>
/// The operator protocol on a user class: len/indexing/in/comparison/arithmetic,
/// a callable instance, truthiness, and the with statement including the value
/// `as` binds. `in`, `==` and `&lt;` report the integer their dunder returned —
/// the same rule every comparison in PyMCU follows, so they read 1/0 where
/// CPython reads True/False.
/// </summary>
[TestFixture]
public class DunderMethodsTests
{
    private SimSession _session = null!;

    [OneTimeSetUp]
    public void BuildFirmware() => _session = new SimSession(PymcuCompiler.BuildFixture("dunder-methods"));

    [Test]
    public void LenIndexAndContains()
    {
        var uno = Sim();
        uno.RunUntilSerial(uno.Serial, "END\n", maxMs: 3000);
        uno.Serial.Should().ContainLine("len=3 idx=5 in=1");
    }

    [Test]
    public void ComparisonArithmeticAndCall()
    {
        var uno = Sim();
        uno.RunUntilSerial(uno.Serial, "END\n", maxMs: 3000);
        uno.Serial.Should().ContainLine("eq=0 lt=1 add=7 call=5");
    }

    [Test]
    public void SetItemMutatesTheInstance()
    {
        var uno = Sim();
        uno.RunUntilSerial(uno.Serial, "END\n", maxMs: 3000);
        uno.Serial.Should().ContainLine("set=8");
    }

    [Test]
    public void TruthinessAsksBool()
    {
        var uno = Sim();
        uno.RunUntilSerial(uno.Serial, "END\n", maxMs: 3000);
        // Box(0) is false, Box(3) is true — an instance evaluated as a bare scalar
        // would report false for both.
        uno.Serial.Should().ContainLine("f0 t1");
    }

    [Test]
    public void WithBindsWhatEnterReturned()
    {
        var uno = Sim();
        uno.RunUntilSerial(uno.Serial, "END\n", maxMs: 3000);
        uno.Serial.Should().ContainLine("with=3");
    }

    private ArduinoUnoSimulation Sim() => _session.Reset();
}
