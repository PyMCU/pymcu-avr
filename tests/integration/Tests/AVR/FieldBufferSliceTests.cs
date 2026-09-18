// SPDX-License-Identifier: MIT
using FluentAssertions;
using NUnit.Framework;
using Avr8Sharp.TestKit.Boards;
using Avr8Sharp.TestKit;

namespace PyMCU.IntegrationTests.Tests.AVR;

/// <summary>
/// fixtures/field-buffer-slice: <c>temp_data = self._buffer[0:2]</c> on a
/// field bytearray. Adafruit sht4x writes that after the I2C read.
///
/// WHAT DISCRIMINATES: prints 10, 40.
/// </summary>
[TestFixture]
public class FieldBufferSliceTests
{
    private static SimSession _session = null!;
    private static SimSession _pySession = null!;

    [OneTimeSetUp]
    public void BuildFirmware()
    {
        _session   = new SimSession(PymcuCompiler.BuildFixture("field-buffer-slice"));
        _pySession = new SimSession(PymcuCompiler.BuildFixturePyParser("field-buffer-slice"));
    }

    private static ArduinoUnoSimulation FullRun(SimSession s)
    {
        var uno = s.Reset();
        uno.RunUntilSerial(uno.Serial, "END\n", maxMs: 4000);
        return uno;
    }

    [Test]
    public void AFieldBytearraySlice_PrintsTheWindowHeads()
    {
        FullRun(_session).Serial.Text.Should().Contain("10\n40\nEND\n",
            because: "head is _buffer[0:2][0] and mid is _buffer[3:5][0]");
    }

    [Test]
    public void TheSameThroughThePythonFrontEnd()
    {
        FullRun(_pySession).Serial.Text.Should().Contain("10\n40\nEND\n",
            because: "both front ends must slice a field bytearray the same way");
    }
}
