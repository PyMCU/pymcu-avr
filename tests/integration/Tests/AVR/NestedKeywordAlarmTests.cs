using Avr8Sharp.TestKit;
using Avr8Sharp.TestKit.Boards;
using FluentAssertions;
using NUnit.Framework;

namespace PyMCU.IntegrationTests.Tests.AVR;

[TestFixture]
public class NestedKeywordAlarmTests
{
    [TestCase(false)]
    [TestCase(true)]
    public void KeywordDeadlineWaitsFiveSeconds(bool withPinAlarm)
    {
        var source = """
            import alarm
            import board
            import time
            from pymcu.types import asm
            from pymcu.chips.atmega328p import GPIOR0

            ta = alarm.time.TimeAlarm(monotonic_time=time.monotonic() + 5)
            pa = alarm.pin.PinAlarm(pin=board.D2, value=False, pull=True)
            GPIOR0.value = alarm.sleep_until_alarms(ALARMS)
            asm("BREAK")
            """.Replace("ALARMS", withPinAlarm ? "ta, pa" : "ta");
        var config = """
            [tool.pymcu]
            board = "arduino_uno"
            frequency = 16000000
            sources = "src"
            entry = "main.py"
            stdlib = ["circuitpython"]
            """;
        var uno = new ArduinoUnoSimulation();
        uno.WithHex(PymcuCompiler.BuildSource(source, config));
        uno.PortD.SetPinValue(2, true);
        uno.RunToBreak(100_000_000);
        uno.Data[0x3E].Should().Be(0, "the time alarm fires while the input remains high");
        (uno.Cpu.Cycles / 16000.0).Should().BeInRange(4990, 5020,
            "PyMCU#443 truncated the nested constructor's uint32 field to one byte");
    }
}
