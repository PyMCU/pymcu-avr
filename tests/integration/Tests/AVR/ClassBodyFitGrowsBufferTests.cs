// SPDX-License-Identifier: MIT
using FluentAssertions;
using NUnit.Framework;
using Avr8Sharp.TestKit.Boards;
using Avr8Sharp.TestKit;

namespace PyMCU.IntegrationTests.Tests.AVR;

/// <summary>
/// fixtures/class-body-fit-grows-buffer: <c>_BUFFER = bytearray(1)</c> then a
/// class-body constructor calls <c>_fit(2)</c> which <c>.extend()</c>s.
/// Adafruit <c>RWBits.__init__</c> is this shape; replaying the declaration
/// used to shrink the grown buffer back to one byte.
///
/// WHAT DISCRIMINATES: <c>7</c> stored at <c>_BUFFER[2]</c>.
/// </summary>
[TestFixture]
public class ClassBodyFitGrowsBufferTests
{
    private static SimSession _session = null!;
    private static SimSession _pySession = null!;

    [OneTimeSetUp]
    public void BuildFirmware()
    {
        _session   = new SimSession(PymcuCompiler.BuildFixture("class-body-fit-grows-buffer"));
        _pySession = new SimSession(PymcuCompiler.BuildFixturePyParser("class-body-fit-grows-buffer"));
    }

    private static ArduinoUnoSimulation FullRun(SimSession s)
    {
        var uno = s.Reset();
        uno.RunUntilSerial(uno.Serial, "END\n", maxMs: 4000);
        return uno;
    }

    [Test]
    public void IndexingPastTheDeclaredSize_ReadsTheGrownSlot()
    {
        FullRun(_session).Serial.Should().ContainLine("7",
            because: "_fit(2) from a class-body constructor must keep the buffer at 3 bytes so _BUFFER[2] stores 7");
    }

    [Test]
    public void TheSameThroughThePythonFrontEnd()
    {
        FullRun(_pySession).Serial.Text.Should().Contain("7\nEND\n",
            because: "both front ends must keep a buffer grown by class-body _fit");
    }
}
