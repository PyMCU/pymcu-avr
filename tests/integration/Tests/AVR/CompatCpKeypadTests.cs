using System.Collections.Generic;
using Avr8Sharp.TestKit;
using Avr8Sharp.TestKit.Boards;
using FluentAssertions;
using NUnit.Framework;

namespace PyMCU.IntegrationTests.Tests.AVR;

/// <summary>
/// fixtures/compat-cp-keypad (pymcu-circuitpython#13): keypad.Keys says which button
/// changed and which way. The module was absent.
///
/// The queue holds no events of its own: a key's stored state moves only when its change is
/// reported, so what is waiting to be read is exactly the set of keys whose pins disagree
/// with it. The test presses buttons and drains the queue three times.
/// </summary>
[TestFixture]
public class CompatCpKeypadTests
{
    private const int Gpior0Addr = 0x3E;
    private const int Gpior1Addr = 0x4A;
    private const int Gpior2Addr = 0x4B;
    private static readonly int[] KeyBits = { 4, 5, 6 };   // D4, D5, D6

    private static SimSession _session = null!;

    [OneTimeSetUp]
    public void BuildFirmware() =>
        _session = new SimSession(PymcuCompiler.BuildFixture("compat-cp-keypad"));

    /// <summary>Holds the named keys down (pulling their pins low), then drains three events.</summary>
    private static (byte Count, List<(byte Key, byte Pressed, byte Waiting)> Events) Run(params int[] pressed)
    {
        var uno = _session.Reset();
        uno.RunToBreak(20_000_000);
        var count = uno.Data[Gpior0Addr];

        // Every key idles high through its pull-up; a press pulls the pin to ground.
        for (var i = 0; i < KeyBits.Length; i++)
            uno.PortD.SetPinValue((byte)KeyBits[i], true);
        foreach (var k in pressed)
            uno.PortD.SetPinValue((byte)KeyBits[k], false);
        uno.RunCycles(160);

        var events = new List<(byte, byte, byte)>();
        for (var i = 0; i < 3; i++)
        {
            uno.RunInstructions(1);
            uno.RunToBreak(20_000_000);
            events.Add((uno.Data[Gpior0Addr], uno.Data[Gpior1Addr], uno.Data[Gpior2Addr]));
        }
        return (count, events);
    }

    [Test]
    public void TheKeyCountIsTheNumberOfPinsGiven()
    {
        Run().Count.Should().Be(3);
    }

    [Test]
    public void NothingPressedIsNoEvent()
    {
        var r = Run();
        r.Events[0].Key.Should().Be(255, "255 is this fixture's way of saying there was no event");
        r.Events[0].Waiting.Should().Be(0);
    }

    [Test]
    public void OnePressIsOneEventAndThenNothing()
    {
        var r = Run(1);
        r.Events[0].Key.Should().Be(1);
        r.Events[0].Pressed.Should().Be(1);
        r.Events[0].Waiting.Should().Be(0, "the change was reported, so nothing is pending");
        r.Events[1].Key.Should().Be(255);
    }

    [Test]
    public void TwoPressesComeOutOneAtATimeInPinOrder()
    {
        var r = Run(0, 2);
        r.Events[0].Key.Should().Be(0);
        r.Events[0].Pressed.Should().Be(1);
        r.Events[0].Waiting.Should().Be(1, "the other key is still waiting to be reported");
        r.Events[1].Key.Should().Be(2);
        r.Events[1].Pressed.Should().Be(1);
        r.Events[1].Waiting.Should().Be(0);
        r.Events[2].Key.Should().Be(255);
    }

    [Test]
    public void EveryKeyCanBePressedAtOnce()
    {
        var r = Run(0, 1, 2);
        r.Events[0].Key.Should().Be(0);
        r.Events[1].Key.Should().Be(1);
        r.Events[2].Key.Should().Be(2);
        r.Events[0].Waiting.Should().Be(2);
    }
}
