// SPDX-License-Identifier: MIT
using FluentAssertions;
using NUnit.Framework;
using Avr8Sharp.TestKit.Boards;
using Avr8Sharp.TestKit;

namespace PyMCU.IntegrationTests.Tests.AVR;

/// <summary>
/// PyMCU/PyMCU#373. A method call on self, from inside another method reached from
/// __init__, used to report the receiver as an integer -- reduced from
/// adafruit_bmp280.py and adafruit_veml7700.py.
/// </summary>
[TestFixture]
public class OutlinedSelfCallOverrideTests
{
    /// <summary>
    /// The discriminating value is 182 (0xB6): the byte the override actually stores.
    /// Silently running Base's own (never-called) version instead would not print anything
    /// here at all -- NotImplementedError propagates unhandled out of module init, and the
    /// firmware would never reach the UART line that prints it.
    /// </summary>
    [Test]
    public void Issue373_TheOverrideRunsNotTheBase()
    {
        var uno = new SimSession(
            PymcuCompiler.BuildFixture("outlined-self-call-override")).Reset();
        uno.RunUntilSerial(uno.Serial, "END\n", maxMs: 400);

        uno.Serial.Text.Should().Contain("OSC\n182\nEND\n");
    }
}
