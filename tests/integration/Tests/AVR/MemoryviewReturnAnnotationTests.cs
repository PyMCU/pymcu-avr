// SPDX-License-Identifier: MIT
using FluentAssertions;
using NUnit.Framework;
using Avr8Sharp.TestKit.Boards;
using Avr8Sharp.TestKit;

namespace PyMCU.IntegrationTests.Tests.AVR;

/// <summary>
/// fixtures/memoryview-return-annotation: <c>def view_of(b: bytearray) -&gt; memoryview</c>
/// then <c>v[0]</c>. Adafruit pca9685's <c>_get_buffer</c> is this annotation;
/// it was refused as an unknown type even though <c>memoryview()</c> already lowers.
///
/// WHAT DISCRIMINATES: <c>3</c> (first byte of the underlying buffer).
/// </summary>
[TestFixture]
public class MemoryviewReturnAnnotationTests
{
    private static SimSession _session = null!;
    private static SimSession _pySession = null!;

    [OneTimeSetUp]
    public void BuildFirmware()
    {
        _session   = new SimSession(PymcuCompiler.BuildFixture("memoryview-return-annotation"));
        _pySession = new SimSession(PymcuCompiler.BuildFixturePyParser("memoryview-return-annotation"));
    }

    private static ArduinoUnoSimulation FullRun(SimSession s)
    {
        var uno = s.Reset();
        uno.RunUntilSerial(uno.Serial, "END\n", maxMs: 4000);
        return uno;
    }

    [Test]
    public void IndexingAnAnnotatedMemoryviewReturn_PrintsTheUnderlyingByte()
    {
        FullRun(_session).Serial.Should().ContainLine("3",
            because: "v[0] through -> memoryview is the first byte of buf");
    }

    [Test]
    public void TheSameThroughThePythonFrontEnd()
    {
        FullRun(_pySession).Serial.Text.Should().Contain("3\nEND\n",
            because: "both front ends must accept memoryview as a return annotation");
    }
}
