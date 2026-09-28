// SPDX-License-Identifier: MIT
using FluentAssertions;
using NUnit.Framework;
using Avr8Sharp.TestKit.Boards;
using Avr8Sharp.TestKit;

namespace PyMCU.IntegrationTests.Tests.AVR;

/// <summary>
/// bool interpolation: a name bound to True/False streams Python's words, both
/// from an f-string and from print(). A bool folded to a constant prints its word
/// with no branch; one decided at runtime picks the word with a branch. A name
/// bound to a comparison is the bool CPython binds, so it spells True/False too
/// -- the frontier is a name that ever holds an integer, which keeps printing
/// digits at every read.
/// </summary>
[TestFixture]
public class FStringBoolTests
{
    private SimSession _session = null!;

    [OneTimeSetUp]
    public void BuildFirmware() => _session = new SimSession(PymcuCompiler.BuildFixture("fstring-bool"));

    [Test]
    public void LiteralBoundName_PrintsTrueFalse()
    {
        var uno = Sim();
        uno.RunUntilSerial(uno.Serial, "flag=True off=False\n", maxMs: 3000);
        uno.Serial.Should().ContainLine("flag=True off=False");
    }

    [Test]
    public void BareLiteral_PrintsTrueFalse()
    {
        var uno = Sim();
        uno.RunUntilSerial(uno.Serial, "lit=True/False\n", maxMs: 3000);
        uno.Serial.Should().ContainLine("lit=True/False");
    }

    [Test]
    public void PrintArgument_PrintsTrueFalse()
    {
        var uno = Sim();
        uno.RunUntilSerial(uno.Serial, "True False\n", maxMs: 3000);
        uno.Serial.Should().ContainLine("True False");
    }

    [Test]
    public void Reassignment_PrintsTheNewWord()
    {
        var uno = Sim();
        uno.RunUntilSerial(uno.Serial, "toggled=False\n", maxMs: 3000);
        uno.Serial.Should().ContainLine("toggled=False");
    }

    [Test]
    public void BoundComparisonName_IsBool_MixedName_StaysNumeric()
    {
        var uno = Sim();
        uno.RunUntilSerial(uno.Serial, "cmp=True mixed=0\n", maxMs: 3000);
        uno.Serial.Should().ContainLine("cmp=True mixed=0");
    }

    [Test]
    public void RuntimeBool_PicksTheWordAtRuntime()
    {
        var uno = Sim();
        uno.RunUntilSerial(uno.Serial, "seen=False\nseen=True\n", maxMs: 3000);
        uno.Serial.Text.Should().Contain("seen=False\nseen=True\n");
    }

    private ArduinoUnoSimulation Sim() => _session.Reset();
}
