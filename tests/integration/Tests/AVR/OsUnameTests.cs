// SPDX-License-Identifier: MIT
using FluentAssertions;
using NUnit.Framework;
using Avr8Sharp.TestKit.Boards;
using Avr8Sharp.TestKit;

namespace PyMCU.IntegrationTests.Tests.AVR;

/// <summary>
/// fixtures/os-uname (PyMCU#466): <c>os.uname()</c> is a compile-time five-field
/// record of the part this firmware was built for. Adafruit DHT's
/// <c>"Linux" not in uname()</c> folds; platformdetect's
/// <c>"RP2350" in uname().machine</c> is a compile-time substring.
///
/// WHAT DISCRIMINATES: <c>1</c> (Linux not in uname), <c>1</c> (this part is not
/// RP2350), <c>1</c> (machine contains atmega328p). A compile that still refused
/// <c>import os</c> would not build.
/// </summary>
[TestFixture]
[Property("Issue", "466")]
public class OsUnameTests
{
    private static SimSession _session = null!;
    private static SimSession _pySession = null!;

    [OneTimeSetUp]
    public void BuildFirmware()
    {
        _session   = new SimSession(PymcuCompiler.BuildFixture("os-uname"));
        _pySession = new SimSession(PymcuCompiler.BuildFixturePyParser("os-uname"));
    }

    private static ArduinoUnoSimulation FullRun(SimSession s)
    {
        var uno = s.Reset();
        uno.RunUntilSerial(uno.Serial, "END\n", maxMs: 4000);
        return uno;
    }

    [Test]
    public void LinuxIsNotInUname()
    {
        FullRun(_session).Serial.Text.Should().StartWith("1\n",
            because: "Adafruit DHT writes 'Linux not in uname()' to pick CircuitPython over Blinka");
    }

    [Test]
    public void MachineIsThisChipAndNotAnRpToken()
    {
        FullRun(_session).Serial.Text.Should().Contain("1\n1\n1\nEND\n",
            because: "uname().machine is __CHIP__.name; substring membership folds at compile time");
    }

    [Test]
    public void TheSameThroughThePythonFrontEnd()
    {
        FullRun(_pySession).Serial.Text.Should().Contain("1\n1\n1\nEND\n",
            because: "both front ends must load pymcu/os.py and fold uname from __CHIP__");
    }
}
