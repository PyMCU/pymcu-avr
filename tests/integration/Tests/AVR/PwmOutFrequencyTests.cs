// SPDX-License-Identifier: MIT
using FluentAssertions;
using NUnit.Framework;
using Avr8Sharp.TestKit.Boards;
using Avr8Sharp.TestKit;

namespace PyMCU.IntegrationTests.Tests.AVR;

/// <summary>
/// fixtures/freq-probe: the upstream Adafruit <c>adafruit_motor.servo</c> driver on an
/// Uno, <c>pwmio.PWMOut(board.D5, duty_cycle=0, frequency=50)</c> -- D5 is Timer0's
/// 256-count pin, so a 50 Hz request lands on the 1024 prescaler and the pin really
/// emits 16000000/(1024*256) = 61 Hz. <c>PWMOut.__init__</c> stores that in
/// <c>self._real_frequency = self._pwm.frequency()</c> -- a nested ZCA call whose inline
/// result temp aliases the folded 61 -- and the <c>frequency</c> property getter returns
/// the field. The fixture reads it at module scope, through <c>servo._pwm_out</c>,
/// inside a function, and through a user class's field.
///
/// WHAT DISCRIMINATES: the read inside <c>show()</c> used to load a dead-stored
/// flattened field and print 0 while module scope folded to 61.
/// </summary>
[TestFixture]
public class PwmOutFrequencyTests
{
    private static SimSession _session = null!;
    private static SimSession _pySession = null!;

    private const string Expected =
        "61\n" +          // print(pwm.frequency)          -- module scope
        "f = 61\n" +      // print("f =", pwm.frequency)   -- interned-string arg
        "61\n" +          // print(s._pwm_out.frequency)   -- through servo's field
        "61\n" +          // f = pwm.frequency; print(f)   -- through a local
        "2998\n" +        // s._min_duty = int(750*61/1e6*0xFFFF)
        "5996\n" +        // s._duty_range = int(2250*61/1e6*0xFFFF - 2998)
        "61\n" +          // show(): print(pwm.frequency) inside a function
        "61\n" +          // h.out.frequency -- PWMOut held by a user class
        "5996\n" +        // s.angle = 90 -> _min_duty + int(0.5 * _duty_range), read back
        "END\n";

    [OneTimeSetUp]
    public void BuildFirmware()
    {
        _session   = new SimSession(PymcuCompiler.BuildFixture("freq-probe"));
        _pySession = new SimSession(PymcuCompiler.BuildFixturePyParser("freq-probe"));
    }

    private static ArduinoUnoSimulation FullRun(SimSession s)
    {
        var uno = s.Reset();
        uno.RunUntilSerial(uno.Serial, "END\n", maxMs: 4000);
        return uno;
    }

    [Test]
    public void Frequency_PrintsTheRealBucket_InEveryScope()
    {
        FullRun(_session).Serial.Text.Should().Contain(Expected,
            because: "_real_frequency is written once by PWMOut.__init__ and must read as the "
                   + "61 Hz bucket wherever the program asks for it, including inside functions");
    }

    [Test]
    public void TheSameThroughThePythonFrontEnd()
    {
        FullRun(_pySession).Serial.Text.Should().Contain(Expected,
            because: "both front ends must keep the property's backing field constant and "
                   + "visible from function scope");
    }
}
