// SPDX-License-Identifier: MIT
using FluentAssertions;
using NUnit.Framework;
using Avr8Sharp.TestKit.Boards;
using Avr8Sharp.TestKit;

namespace PyMCU.IntegrationTests.Tests.AVR;

/// <summary>
/// fixtures/framebuf-slice-assign: <c>buf[i:i+3] = bytes(fill)</c>
/// copies three bytes at a run-time start. Adafruit framebuf RGB888
/// fill writes that after <c>fill = (color&gt;&gt;16)&amp;255, ...</c>.
///
/// WHAT DISCRIMINATES: prints 17, 34, 51 twice (0x11, 0x22, 0x33).
/// </summary>
[TestFixture]
public class FramebufSliceAssignTests
{
    private static SimSession _session = null!;
    private static SimSession _pySession = null!;

    [OneTimeSetUp]
    public void BuildFirmware()
    {
        _session   = new SimSession(PymcuCompiler.BuildFixture("framebuf-slice-assign"));
        _pySession = new SimSession(PymcuCompiler.BuildFixturePyParser("framebuf-slice-assign"));
    }

    private static ArduinoUnoSimulation FullRun(SimSession s)
    {
        var uno = s.Reset();
        uno.RunUntilSerial(uno.Serial, "END\n", maxMs: 4000);
        return uno;
    }

    [Test]
    public void BytesFillAtRuntimeStart_StoresEachByte()
    {
        FullRun(_session).Serial.Text.Should().Contain("17\n34\n51\n17\n34\n51\nEND\n",
            because: "buf[i:i+3] = bytes(fill) of 0x112233 writes 0x11, 0x22, 0x33 at i and at i+3");
    }

    [Test]
    public void TheSameThroughThePythonFrontEnd()
    {
        FullRun(_pySession).Serial.Text.Should().Contain("17\n34\n51\n17\n34\n51\nEND\n",
            because: "both front ends must copy bytes(fill) onto a bytearray slice of length 3");
    }
}
