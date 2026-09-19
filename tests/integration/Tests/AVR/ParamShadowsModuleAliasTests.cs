// SPDX-License-Identifier: MIT
using FluentAssertions;
using NUnit.Framework;
using Avr8Sharp.TestKit.Boards;
using Avr8Sharp.TestKit;

namespace PyMCU.IntegrationTests.Tests.AVR;

/// <summary>
/// fixtures/param-shadows-module-alias: <c>import adafruit_framebuf as
/// framebuf</c> then <c>@staticmethod def set_pixel(framebuf, ...)</c>
/// reads <c>framebuf.stride</c>. Adafruit ssd1306 writes that import;
/// MVLSBFormat names the FrameBuffer parameter <c>framebuf</c>.
///
/// WHAT DISCRIMINATES: prints 7.
/// </summary>
[TestFixture]
public class ParamShadowsModuleAliasTests
{
    private static SimSession _session = null!;
    private static SimSession _pySession = null!;

    [OneTimeSetUp]
    public void BuildFirmware()
    {
        _session   = new SimSession(PymcuCompiler.BuildFixture("param-shadows-module-alias"));
        _pySession = new SimSession(PymcuCompiler.BuildFixturePyParser("param-shadows-module-alias"));
    }

    private static ArduinoUnoSimulation FullRun(SimSession s)
    {
        var uno = s.Reset();
        uno.RunUntilSerial(uno.Serial, "END\n", maxMs: 4000);
        return uno;
    }

    [Test]
    public void AParamNamedLikeTheImportAlias_PrintsTheInstanceField()
    {
        FullRun(_session).Serial.Text.Should().Contain("7\nEND\n",
            because: "framebuf.stride inside set_pixel is the instance field, not a module member");
    }

    [Test]
    public void TheSameThroughThePythonFrontEnd()
    {
        FullRun(_pySession).Serial.Text.Should().Contain("7\nEND\n",
            because: "both front ends must let a parameter shadow the import alias");
    }
}
