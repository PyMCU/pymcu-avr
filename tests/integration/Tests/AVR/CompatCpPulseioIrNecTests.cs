using System.Collections.Generic;
using Avr8Sharp.TestKit;
using Avr8Sharp.TestKit.Boards;
using FluentAssertions;
using NUnit.Framework;

namespace PyMCU.IntegrationTests.Tests.AVR;

/// <summary>
/// fixtures/compat-cp-pulseio-ir-nec (pymcu-circuitpython#9): a whole infrared NEC frame
/// driven onto D2 and decoded by the firmware out of pulseio.PulseIn. This is the case the
/// module exists for, end to end: 67 pulses, an address and a command.
/// </summary>
[TestFixture]
public class CompatCpPulseioIrNecTests
{
    private const int Gpior0Addr = 0x3E;
    private const int Gpior1Addr = 0x4A;
    private const int Gpior2Addr = 0x4B;
    private const int D2Bit = 2;

    private static SimSession _session = null!;

    [OneTimeSetUp]
    public void BuildFirmware() =>
        _session = new SimSession(PymcuCompiler.BuildFixture("compat-cp-pulseio-ir-nec"));

    /// <summary>The alternating mark/space durations of an NEC frame, in microseconds.</summary>
    private static List<int> NecFrame(byte address, byte command)
    {
        var us = new List<int> { 9000, 4500 };
        var bytes = new[] { address, (byte)~address, command, (byte)~command };
        foreach (var b in bytes)
            for (var bit = 0; bit < 8; bit++)
            {
                us.Add(560);                                   // the mark
                us.Add(((b >> bit) & 1) == 1 ? 1690 : 560);    // the space carries the bit
            }
        us.Add(560);                                           // the stop mark
        return us;
    }

    private static (byte Count, byte Address, byte Command) Run(byte address, byte command)
    {
        var uno = _session.Reset();
        uno.RunToBreak(80_000_000);

        uno.PortD.SetPinValue(D2Bit, false);
        uno.RunCycles(1600);
        var level = true;
        foreach (var us in NecFrame(address, command))
        {
            uno.PortD.SetPinValue(D2Bit, level);
            uno.RunCycles(us * 16);
            level = !level;
        }
        uno.PortD.SetPinValue(D2Bit, false);

        uno.RunInstructions(1);
        uno.RunToBreak(80_000_000);
        return (uno.Data[Gpior0Addr], uno.Data[Gpior1Addr], uno.Data[Gpior2Addr]);
    }

    [Test]
    public void AWholeFrameArrives()
    {
        // The leader's two, 64 for the bits, and the stop mark's leading interval.
        Run(0x04, 0x08).Count.Should().BeInRange(66, 70);
    }

    [TestCase((byte)0x04, (byte)0x08)]
    [TestCase((byte)0x00, (byte)0xFF)]
    [TestCase((byte)0xFF, (byte)0x00)]
    [TestCase((byte)0xA5, (byte)0x5A)]
    public void TheAddressAndCommandComeBackOut(byte address, byte command)
    {
        var r = Run(address, command);
        r.Address.Should().Be(address);
        r.Command.Should().Be(command);
    }
}
