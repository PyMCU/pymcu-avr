using System.Collections.Generic;
using AVR8Sharp.Core.Peripherals;
using Avr8Sharp.TestKit;
using Avr8Sharp.TestKit.Boards;
using FluentAssertions;
using NUnit.Framework;

namespace PyMCU.IntegrationTests.Tests.AVR;

/// <summary>
/// fixtures/compat-cp-bitbangio-i2c (pymcu-circuitpython#10): a software I2C bus on two
/// ordinary pins. The module was absent, and the soft I2C it wraps had been in the HAL all
/// along; an ATmega has one TWI, so a board with two sensors at the same address needs this.
///
/// The transfer is decoded out of the waveform on D2 and D3, which is the only place a
/// bit-banged bus exists: there is no peripheral register to read it back from.
/// </summary>
[TestFixture]
public class CompatCpBitbangioI2cTests
{
    private const int SclBit = 2;   // D2 = PD2
    private const int SdaBit = 3;   // D3 = PD3

    private static SimSession _session = null!;

    [OneTimeSetUp]
    public void BuildFirmware() =>
        _session = new SimSession(PymcuCompiler.BuildFixture("compat-cp-bitbangio-i2c"));

    private sealed class Trace
    {
        public bool IdleHigh;
        public int Starts;
        public int Stops;
        public List<int> ClockedBits = new();
        public double ClockPeriodUs;
    }

    private static Trace Run()
    {
        var uno = _session.Reset();
        uno.RunToBreak(10_000_000);
        var t = new Trace
        {
            IdleHigh = uno.PortD.GetPinState((byte)SclBit) == PinState.High
                       && uno.PortD.GetPinState((byte)SdaBit) == PinState.High,
        };

        uno.RunInstructions(1);
        var start = uno.Cpu.Cycles;
        var prevScl = uno.PortD.GetPinState((byte)SclBit) == PinState.High;
        var prevSda = uno.PortD.GetPinState((byte)SdaBit) == PinState.High;
        var rises = new List<double>();
        for (var i = 0; i < 30000; i++)
        {
            uno.RunCycles(4);
            var scl = uno.PortD.GetPinState((byte)SclBit) == PinState.High;
            var sda = uno.PortD.GetPinState((byte)SdaBit) == PinState.High;
            var now = (uno.Cpu.Cycles - start) / 16.0;
            if (scl && !prevScl) { rises.Add(now); t.ClockedBits.Add(sda ? 1 : 0); }
            if (scl && prevSda && !sda) t.Starts++;
            if (scl && !prevSda && sda) t.Stops++;
            prevScl = scl; prevSda = sda;
        }
        if (rises.Count > 2) t.ClockPeriodUs = rises[2] - rises[1];
        return t;
    }

    private static int Byte(List<int> bits, int from)
    {
        var v = 0;
        for (var i = from; i < from + 8; i++) v = (v << 1) | bits[i];
        return v;
    }

    [Test]
    public void BothLinesIdleHigh()
    {
        // Open-drain: neither pin is ever driven high, only released to the pull-up.
        Run().IdleHigh.Should().BeTrue();
    }

    [Test]
    public void TheTransferIsFramedByAStartAndAStop()
    {
        var t = Run();
        t.Starts.Should().Be(1);
        t.Stops.Should().Be(1);
    }

    [Test]
    public void TheAddressAndThePayloadAreClockedOut()
    {
        var t = Run();
        t.ClockedBits.Count.Should().BeGreaterThanOrEqualTo(18, "eight address bits, an ACK, eight data bits and an ACK");
        Byte(t.ClockedBits, 0).Should().Be(0xD0, "0x68 shifted left with the write bit clear");
        Byte(t.ClockedBits, 9).Should().Be(0xA5, "the payload");
    }

    [Test]
    public void TheClockIsNearTheRateAskedFor()
    {
        // 100 kHz asked. The half-period is a whole microsecond and the bit loop costs time
        // of its own on top of it, so a software bus runs under its nominal rate.
        var period = Run().ClockPeriodUs;
        period.Should().BeInRange(10, 14, "100 kHz asked comes out near 85 kHz");
    }
}
