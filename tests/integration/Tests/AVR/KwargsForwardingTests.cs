// SPDX-License-Identifier: MIT
using FluentAssertions;
using NUnit.Framework;
using Avr8Sharp.TestKit.Boards;
using Avr8Sharp.TestKit;

namespace PyMCU.IntegrationTests.Tests.AVR;

/// <summary>
/// `**kwargs` and `*args` are compile-time mappings and sequences (#368).
///
/// The forwarding chain is the case that moves the Adafruit corpus: adafruit_debouncer's
/// Button collects keyword arguments and hands them to Debouncer through
/// `super().__init__(pin, **kwargs)`, and adafruit_74hc595 and adafruit_pcf8574 collect them
/// only to discard them, so that switch_to_output() matches digitalio.DigitalInOut's
/// signature. All three stopped at the `def` and reported nothing else.
///
/// WHAT DISCRIMINATES: the three-level chain in `a`, where each level consumes one named
/// parameter and passes the rest on. A splice that forwards the whole mapping unchanged, or
/// that loses a key at the second hop, gets a different line.
///
/// WHAT IS INVARIANT: `b.retries`, the default two levels down that nothing ever writes. It
/// is what separates "the default was applied" from "the mapping happened to be empty".
/// </summary>
[TestFixture]
public class KwargsForwardingTests
{
    private SimSession _session = null!;

    [OneTimeSetUp]
    public void BuildFirmware() => _session = new SimSession(PymcuCompiler.BuildFixture("kwargs-forwarding"));

    [Test]
    public void ThreeLevelsOfForwardingReachEveryNamedParameter()
    {
        var uno = FullRun();

        // pin, long_ms by position, short_ms one level down, interval_ms two levels down,
        // retries never written.
        uno.Serial.Should().ContainLine("a=7,50,25,9,3");
    }

    [Test]
    public void KeywordsCollectedOnlyToBeDiscardedCostNothingAndChangeNothing()
    {
        var uno = FullRun();

        uno.Serial.Should().ContainLine("c=1");
    }

    [Test]
    public void TheReadsOverAKeywordMappingFold()
    {
        var uno = FullRun();

        // len(kwargs) + kwargs["a"] + kwargs.get("b", 5), with and without the key present.
        uno.Serial.Should().ContainLine("d=5,10");
    }

    [Test]
    public void ItemsUnrollsOverTheKnownKeys()
    {
        var uno = FullRun();

        uno.Serial.Should().ContainLine("e=6");
    }

    [Test]
    public void VariadicPositionsUnrollAndCount()
    {
        var uno = FullRun();

        uno.Serial.Should().ContainLine("f=6,3");
    }

    [Test]
    public void AKeywordValueThatIsNotALiteralSurvivesTheHop()
    {
        // `interval_ms=scaled(m)` arrived as 0. The value is computed in the caller and has to
        // be carried across the binding, and the name holding it was written unqualified there
        // and read qualified inside the expansion: two different variables, one of them never
        // written. A literal and a bare name both worked, which is why the shape had to be
        // named on its own rather than assumed covered.
        var uno = FullRun();

        uno.Serial.Should().ContainLine("g=9");
        uno.Serial.Should().NotContain("g=0");
    }

    private ArduinoUnoSimulation FullRun(int maxMs = 5000)
    {
        var uno = _session.Reset();
        uno.RunUntilSerial(uno.Serial, "DONE\n", maxMs: maxMs);
        return uno;
    }
}
