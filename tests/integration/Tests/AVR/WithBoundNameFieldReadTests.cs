// SPDX-License-Identifier: MIT
using FluentAssertions;
using NUnit.Framework;
using Avr8Sharp.TestKit.Boards;
using Avr8Sharp.TestKit;

namespace PyMCU.IntegrationTests.Tests.AVR;

/// <summary>
/// PyMCU/PyMCU#390. A with-block's bound name reads zero instead of the object's real field
/// values (or, for a method call on it inside a force-inlined method -- the shape
/// adafruit_bmp280.py:463 stops on -- has no class at all).
/// </summary>
[TestFixture]
public class WithBoundNameFieldReadTests
{
    /// <summary>
    /// The discriminating values are exactly what CPython prints for this program: 1 (what
    /// __enter__ just set), 3 (1 + 2, both fields read correctly through their bound names),
    /// 11 and 12 (each field after its own __exit__ ran), and 7 (5 + 2, a method call
    /// dispatched correctly on a bound name inside a force-inlined method).
    /// </summary>
    [Test]
    public void Issue390_EveryWithBoundNameReadsTheRealFieldValue()
    {
        var uno = new SimSession(
            PymcuCompiler.BuildFixture("with-bound-name-field-read")).Reset();
        uno.RunUntilSerial(uno.Serial, "END\n", maxMs: 400);

        uno.Serial.Text.Should().Contain("WB\n1\n3\n11\n12\n7\nEND\n");
    }
}
