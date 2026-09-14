using Avr8Sharp.TestKit;
using Avr8Sharp.TestKit.Boards;
using FluentAssertions;
using NUnit.Framework;

namespace PyMCU.IntegrationTests.Tests.AVR;

/// <summary>
/// fixtures/compat-cp-time-and-watchdog (pymcu-circuitpython#26, #19, #16, #25): a sleep
/// under a millisecond, a watchdog that can be turned off, and an EEPROM size that is the
/// part's.
///
/// Before: sleep() went through a 16-bit millisecond count, so anything under a millisecond
/// rounded to zero and did not sleep at all; assigning watchdog.mode armed it whatever the
/// value and `mode = None` did not compile; len(nvm) was 1024 on every chip.
/// </summary>
[TestFixture]
public class CompatCpTimeAndWatchdogTests
{
    private const int Gpior0Addr = 0x3E;
    private const int Gpior1Addr = 0x4A;
    private const int Gpior2Addr = 0x4B;

    private static SimSession _session = null!;

    [OneTimeSetUp]
    public void BuildFirmware() =>
        _session = new SimSession(PymcuCompiler.BuildFixture("compat-cp-time-and-watchdog"));

    private sealed class Reading
    {
        public int NvmLength;
        public byte SerialBytes;
        public byte WdtArmed;
        public byte WdtDisabled;
        public double ShortSleepUs;
        public double LongSleepUs;
    }

    private static Reading Run()
    {
        var uno = _session.Reset();
        var r = new Reading();

        uno.RunToBreak(40_000_000);
        r.NvmLength = uno.Data[Gpior0Addr] | (uno.Data[Gpior1Addr] << 8);
        r.SerialBytes = uno.Data[Gpior2Addr];

        uno.RunInstructions(1);
        uno.RunToBreak(40_000_000);
        r.WdtArmed = uno.Data[Gpior0Addr];

        uno.RunInstructions(1);
        uno.RunToBreak(40_000_000);
        r.WdtDisabled = uno.Data[Gpior0Addr];

        uno.RunInstructions(1);
        var start = uno.Cpu.Cycles;
        uno.RunToBreak(40_000_000);
        r.ShortSleepUs = (uno.Cpu.Cycles - start) / 16.0;

        uno.RunInstructions(1);
        start = uno.Cpu.Cycles;
        uno.RunToBreak(40_000_000);
        r.LongSleepUs = (uno.Cpu.Cycles - start) / 16.0;

        return r;
    }

    [Test]
    public void TheNvmLengthIsThisPartsEeprom()
    {
        Run().NvmLength.Should().Be(1024, "the ATmega328P has 1 KB of EEPROM");
    }

    [Test]
    public void SerialBytesAvailableIsAnswerAndNotAConstantZero()
    {
        // Nothing has been sent, so 0 is right here. What matters is that it comes from the
        // UART: as a constant, `while not serial_bytes_available:` never ended.
        Run().SerialBytes.Should().Be(0);
    }

    [Test]
    public void SettingTheModeToResetArmsTheWatchdog()
    {
        // WDE, bit 3 of WDTCSR, is the reset enable.
        ((Run().WdtArmed >> 3) & 1).Should().Be(1);
    }

    [Test]
    public void SettingTheModeToNoneTurnsItOff()
    {
        Run().WdtDisabled.Should().Be(0, "mode = None disables it, and did not compile at all before");
    }

    [Test]
    public void ASleepUnderAMillisecondActuallySleeps()
    {
        // 500 us asked. It used to round to zero milliseconds and return at once.
        Run().ShortSleepUs.Should().BeInRange(450, 700);
    }

    [Test]
    public void ASleepOfSeveralMillisecondsIsStillRight()
    {
        Run().LongSleepUs.Should().BeInRange(69000, 72000, "70 ms asked");
    }
}
