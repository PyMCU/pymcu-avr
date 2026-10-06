using Avr8Sharp.TestKit;
using Avr8Sharp.TestKit.Boards;
using FluentAssertions;
using NUnit.Framework;

namespace PyMCU.IntegrationTests.Tests.AVR;

/// <summary>
/// fixtures/compat-cp-uart-timeout-seconds: busio.UART's timeout is seconds for every
/// numeric spelling, like CircuitPython's mp_obj_get_float coercion -- timeout=1 is one
/// second and reads back 1.0. The layer used to treat an int as milliseconds, so the
/// getter printed 1 for a 1 ms timeout.
/// </summary>
[TestFixture]
public class CompatCpUartTimeoutTests
{
    [TestCase(false)]
    [TestCase(true)]
    public void Uart_Timeout_ReadsBackSeconds(bool pyParser)
    {
        var uno = new ArduinoUnoSimulation();
        uno.WithHex(pyParser
            ? PymcuCompiler.BuildFixturePyParser("compat-cp-uart-timeout-seconds")
            : PymcuCompiler.BuildFixture("compat-cp-uart-timeout-seconds"));
        uno.RunMilliseconds(2000);
        uno.Serial.Text.Replace("\r\n", "\n").Should().Be("1.0\n0.25\n",
            "timeout=1 reads back 1.0 seconds and the setter takes the same spelling");
    }
}
