// SPDX-License-Identifier: MIT
using FluentAssertions;
using NUnit.Framework;
using Avr8Sharp.TestKit.Boards;
using Avr8Sharp.TestKit;

namespace PyMCU.IntegrationTests.Tests.AVR;

/// <summary>
/// fixtures/memoryview-slice-view: <c>memoryview(self.buffer)[1:]</c>
/// through I2C -&gt; _SSD1306(buffer) -&gt; FrameBuffer(buffer), then
/// <c>len(framebuf.buf)</c> / <c>framebuf.buf[i] = color</c>. Adafruit
/// ssd1306 writes that so byte 0 stays the I2C command.
///
/// WHAT DISCRIMINATES: prints 3, 64, 7, 7, 7.
/// </summary>
[TestFixture]
public class MemoryviewSliceViewTests
{
    private static SimSession _session = null!;
    private static SimSession _pySession = null!;

    [OneTimeSetUp]
    public void BuildFirmware()
    {
        _session   = new SimSession(PymcuCompiler.BuildFixture("memoryview-slice-view"));
        _pySession = new SimSession(PymcuCompiler.BuildFixturePyParser("memoryview-slice-view"));
    }

    private static ArduinoUnoSimulation FullRun(SimSession s)
    {
        var uno = s.Reset();
        uno.RunUntilSerial(uno.Serial, "END\n", maxMs: 4000);
        return uno;
    }

    [Test]
    public void AMemoryviewSliceThroughSuper_FillsFromOffset()
    {
        FullRun(_session).Serial.Text.Should().Contain("3\n64\n7\n7\n7\nEND\n",
            because: "len is the window; buffer[0] stays 64; fill writes buffer[1:]");
    }

    [Test]
    public void TheSameThroughThePythonFrontEnd()
    {
        FullRun(_pySession).Serial.Text.Should().Contain("3\n64\n7\n7\n7\nEND\n",
            because: "both front ends must treat memoryview(buf)[1:] as a writable view");
    }
}
