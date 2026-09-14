using Avr8Sharp.TestKit;
using Avr8Sharp.TestKit.Boards;
using FluentAssertions;
using NUnit.Framework;

namespace PyMCU.IntegrationTests.Tests.AVR;

/// <summary>
/// fixtures/compat-cp-alarm (pymcu-circuitpython#20): sleep_until_alarms waits on more than
/// one alarm and says which fired. It took one alarm and returned a constant 0.
///
/// A TimeAlarm also starts the millisecond time base itself now: the build starts that clock
/// only for a program that names ticks_ms, monotonic or asyncio, so an alarm was waiting on
/// a counter that never moved and the call never returned.
/// </summary>
[TestFixture]
public class CompatCpAlarmTests
{
    private const int Gpior0Addr = 0x3E;
    private const int D2Bit = 2;

    private static SimSession _session = null!;

    [OneTimeSetUp]
    public void BuildFirmware() =>
        _session = new SimSession(PymcuCompiler.BuildFixture("compat-cp-alarm"));

    private static (byte Which, double Ms) Run(bool pinHigh)
    {
        var uno = _session.Reset();
        uno.PortD.SetPinValue(D2Bit, pinHigh);
        var start = uno.Cpu.Cycles;
        uno.RunToBreak(60_000_000);
        return (uno.Data[Gpior0Addr], (uno.Cpu.Cycles - start) / 16000.0);
    }

    [Test]
    public void TheTimeAlarmFiresWhenThePinStaysLow()
    {
        var r = Run(false);
        r.Which.Should().Be(0, "the first alarm in the list is the time one");
        r.Ms.Should().BeInRange(49, 55, "the alarm was set 50 ms out");
    }

    [Test]
    public void ThePinAlarmFiresFirstWhenThePinIsAlreadyHigh()
    {
        var r = Run(true);
        r.Which.Should().Be(1, "the second alarm in the list is the pin one");
        r.Ms.Should().BeLessThan(10, "the pin is already at the level asked for");
    }
}
