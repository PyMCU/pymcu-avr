// SPDX-License-Identifier: MIT
using FluentAssertions;
using NUnit.Framework;
using Avr8Sharp.TestKit.Boards;
using Avr8Sharp.TestKit;

namespace PyMCU.IntegrationTests.Tests.AVR;

/// <summary>
/// PyMCU/PyMCU#376. An annotation naming an enum MEMBER, not the enum type --
/// `digitalio.Direction.OUTPUT` -- stopped adafruit_74hc595 and adafruit_pcf8574 at the same
/// shape: a property annotated with the value it actually returns.
/// </summary>
[TestFixture]
public class EnumMemberAnnotationTests
{
    /// <summary>
    /// The discriminating values are what CPython itself prints: 1 (digitalio.Direction
    /// .OUTPUT's own value) twice -- once read directly, once through the comparison the
    /// annotation must not have changed the meaning of.
    /// </summary>
    [Test]
    public void Issue376_TheAnnotatedPropertyReadsAndComparesLikeCPython()
    {
        var uno = new SimSession(
            PymcuCompiler.BuildFixture("enum-member-annotation")).Reset();
        uno.RunUntilSerial(uno.Serial, "END\n", maxMs: 400);

        uno.Serial.Text.Should().Contain("EMA\n1\n1\nEND\n");
    }
}
