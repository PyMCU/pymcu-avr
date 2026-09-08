using FluentAssertions;
using NUnit.Framework;
using Avr8Sharp.TestKit.Boards;
using Avr8Sharp.TestKit;

namespace PyMCU.IntegrationTests.Tests.AVR;

/// <summary>
/// Integration tests for fixtures/avr/compat-cp-alarm.
/// Verifies the pymcu-circuitpython alarm sleep entry points run on real AVR:
/// alarm.light_sleep_until_alarms(), alarm.exit_and_deep_sleep_until_alarms() and
/// alarm.sleep_until_alarms() each block on a TimeAlarm (~50 ms) and return.
/// Expected UART: "ABCDE".
///
/// The third is the DIRECT call and it is asserted alongside the other two on
/// purpose (PyMCU#271): it used to fail while its own wrappers compiled, because
/// `alarm.time.TimeAlarm(...)` -- a class nested in a module-level namespace --
/// left the named variable untagged and only the anonymous constructor temp
/// carried the class. Asserting one direction would not notice a change that
/// fixed it by breaking the other.
///
/// This also covers nested-class ZCA construction (alarm.time.TimeAlarm, a class
/// nested in a module-level namespace) compiling on-target.
/// </summary>
[TestFixture]
public class CompatCpAlarmTests
{
    private SimSession _session = null!;

    [OneTimeSetUp]
    public void BuildFirmware() => _session = new SimSession(PymcuCompiler.BuildFixture("compat-cp-alarm"));

    [Test]
    public void LightSleep_WakesAndContinues()
    {
        var uno = Sim();
        uno.RunUntilSerial(uno.Serial, "B", maxMs: 500);
        uno.Serial.Should().Contain("B", "light_sleep_until_alarms() returns and the program continues");
    }

    /// The direct call, which is the one PyMCU#271 refused. Separate from the
    /// ordering test so a regression names which entry point came back.
    [Test]
    public void DirectSleepUntilAlarms_WakesAndContinues()
    {
        var uno = Sim();
        uno.RunUntilSerial(uno.Serial, "D", maxMs: 1500);
        uno.Serial.Should().Contain("D", "sleep_until_alarms() returns and the program continues");
    }

    [Test]
    public void DeepSleepEntry_ReturnsAndReachesDone()
    {
        var uno = Sim();
        uno.RunUntilSerial(uno.Serial, "E", maxMs: 1500);
        uno.Serial.Text.Should().Contain("ABCDE",
            "all three sleep entry points run in order and the firmware reaches the done marker");
    }

    private ArduinoUnoSimulation Sim() => _session.Reset();
}
