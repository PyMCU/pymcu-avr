// SPDX-License-Identifier: MIT
using FluentAssertions;
using NUnit.Framework;
using Avr8Sharp.TestKit.Boards;
using Avr8Sharp.TestKit;

namespace PyMCU.IntegrationTests.Tests.AVR;

/// <summary>
/// The '=' debug spelling of an f-string (issue #185). `f"{seed=}"` is written to label a
/// value, and the label was the half that went missing: the program printed a bare number,
/// silently, and the serial line still read like a log line. These assertions are on the text
/// the running program writes, because a build-success assertion passes on the defect.
/// </summary>
[TestFixture]
public class FStringDebugSpecTests
{
    private SimSession _session = null!;

    [OneTimeSetUp]
    public void BuildFirmware() => _session = new SimSession(PymcuCompiler.BuildFixture("fstring-debug"));

    [Test]
    public void DebugSpec_PrintsTheLabelAndTheValue()
    {
        var uno = Sim();
        uno.RunUntilSerial(uno.Serial, "seed=17\n", maxMs: 3000);
        uno.Serial.Should().ContainLine("seed=17");
    }

    [Test]
    public void DebugSpec_KeepsTheSpacingAsWritten()
    {
        var uno = Sim();
        uno.RunUntilSerial(uno.Serial, "seed = 17\n", maxMs: 3000);
        uno.Serial.Should().ContainLine("seed = 17");
    }

    [Test]
    public void DebugSpec_LabelsEachFieldOfALine()
    {
        // The shape that makes the loss unreadable: dropped, this prints "250 17" and the
        // reader has to guess which number is which.
        var uno = Sim();
        uno.RunUntilSerial(uno.Serial, "count=250 seed=17\n", maxMs: 3000);
        uno.Serial.Should().ContainLine("count=250 seed=17");
    }

    [Test]
    public void DebugSpec_LabelsAnExpressionWithItsSource()
    {
        var uno = Sim();
        uno.RunUntilSerial(uno.Serial, "add(seed, 22)=39\n", maxMs: 3000);
        uno.Serial.Should().ContainLine("add(seed, 22)=39");
    }

    [Test]
    public void DebugSpec_CombinesWithAFormatSpec()
    {
        // The label is the text, the spec still governs the value: 17 as two hex digits.
        var uno = Sim();
        uno.RunUntilSerial(uno.Serial, "seed=11\n", maxMs: 3000);
        uno.Serial.Should().ContainLine("seed=11");
    }

    [Test]
    public void EqualsInsideAQuotedRun_IsNotADebugSpec()
    {
        // Here the '=' belongs to a string the field prints, and labelling it would print
        // "'a=b'=a=b" instead.
        var uno = Sim();
        uno.RunUntilSerial(uno.Serial, "a=b\n", maxMs: 3000);
        uno.Serial.Should().ContainLine("a=b");
    }

    private ArduinoUnoSimulation Sim() => _session.Reset();
}
