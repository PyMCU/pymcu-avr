using FluentAssertions;
using NUnit.Framework;

namespace PyMCU.IntegrationTests.Tests.AVR;

/// <summary>
/// Integration test for fixtures/sleep-seconds.
///
/// `from time import sleep` with `sleep(0.5)` is the first line of the first Python
/// program, and it used to be an ImportError pointing at a library nobody can install
/// (PyMCU#52). The seconds fold to the existing millisecond delay at compile time.
///
/// The assertion is equivalence rather than size: the firmware must be the same image
/// as the blink written with delay_ms(500). A sleep that folded to the wrong number --
/// or to nothing -- would still build, and would still be "small".
/// </summary>
[TestFixture]
public class SleepSecondsTests
{
    private const string DelayMsEquivalent =
        "from pymcu.hal.gpio import Pin\n" +
        "from pymcu.time import delay_ms\n" +
        "\n" +
        "\n" +
        "def main():\n" +
        "    led = Pin(\"PB5\", Pin.OUT)\n" +
        "    while True:\n" +
        "        led.high()\n" +
        "        delay_ms(500)\n" +
        "        led.low()\n" +
        "        delay_ms(500)\n";

    [Test]
    public void SleepInSeconds_IsTheSameImageAsDelayMs()
    {
        var withSleep = PymcuCompiler.BuildFixture("sleep-seconds");
        var withDelayMs = PymcuCompiler.BuildSource(DelayMsEquivalent);

        withSleep.Should().Be(withDelayMs,
            "sleep(0.5) must fold to exactly delay_ms(500), not merely compile");
    }
}
