// SPDX-License-Identifier: MIT
using FluentAssertions;
using NUnit.Framework;
using Avr8Sharp.TestKit.Boards;
using Avr8Sharp.TestKit;

namespace PyMCU.IntegrationTests.Tests.AVR;

/// <summary>
/// A MicroPython driver forwards keyword arguments to a base class (#368).
///
/// machine.Signal(pin, invert=0) is the natural base in this layer, and a user driver on top
/// of it writes the same idiom adafruit_debouncer writes on top of Debouncer: take the pin,
/// and hand whatever else the base understands on through `super().__init__(..., **kwargs)`.
/// The driver never names `invert`.
///
/// This is the same compile-time splice as `kwargs-forwarding`, reached through a base
/// `__init__` that is `@inline` and takes a ZCA Pin instance, which the pure-PyMCU fixture
/// does not exercise.
///
/// WHAT DISCRIMINATES: `g=1,0`. `invert=1` travelled two hops without being named at the
/// first, so the logical value read back is the inverse of the level on the pin.
///
/// WHAT IS INVARIANT: `h=1,0`, the same driver constructed with no keywords at all, where
/// the base's own default has to survive an empty mapping.
/// </summary>
[TestFixture]
public class CompatMpKwargsForwardingTests
{
    private SimSession _session = null!;

    [OneTimeSetUp]
    public void BuildFirmware() =>
        _session = new SimSession(PymcuCompiler.BuildFixture("compat-mp-kwargs-forwarding"));

    [Test]
    public void AKeywordTheDriverNeverNamesReachesTheBase()
    {
        var uno = FullRun();

        uno.Serial.Should().ContainLine("g=1,0");
    }

    [Test]
    public void TheBaseDefaultSurvivesAnEmptyMapping()
    {
        var uno = FullRun();

        uno.Serial.Should().ContainLine("h=1,0");
    }

    private ArduinoUnoSimulation FullRun(int maxMs = 5000)
    {
        var uno = _session.Reset();
        uno.RunUntilSerial(uno.Serial, "DONE\n", maxMs: maxMs);
        return uno;
    }
}
