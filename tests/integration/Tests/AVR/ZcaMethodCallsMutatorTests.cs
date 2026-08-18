// SPDX-License-Identifier: MIT
using FluentAssertions;
using NUnit.Framework;
using Avr8Sharp.TestKit.Boards;
using Avr8Sharp.TestKit;

namespace PyMCU.IntegrationTests.Tests.AVR;

/// <summary>
/// A ZCA method that reaches its field through a sibling method. A single-field
/// (Model A) instance passes its field to the shared body BY VALUE, so a sibling
/// that mutates it has to hand the new value back — through the caller's own field
/// parameter, and on out to the instance. Without that the increment landed on a
/// copy and the counter never moved, silently.
/// </summary>
[TestFixture]
public class ZcaMethodCallsMutatorTests
{
    private SimSession _session = null!;

    [OneTimeSetUp]
    public void BuildFirmware() =>
        _session = new SimSession(PymcuCompiler.BuildFixture("zca-method-calls-mutator"));

    [Test]
    public void MutationThroughASiblingMethodSurvives()
    {
        var uno = Sim();
        uno.RunUntilSerial(uno.Serial, "w1=5 w2=5 w3=5\n", maxMs: 3000);
        // w1: bump() -> inc(); w2: assignment from a read-only sibling (the control);
        // w3: two inc() calls in one method, so the second sees the first.
        uno.Serial.Should().ContainLine("w1=5 w2=5 w3=5");
    }

    private ArduinoUnoSimulation Sim() => _session.Reset();
}
