// SPDX-License-Identifier: MIT
using FluentAssertions;
using NUnit.Framework;
using Avr8Sharp.TestKit.Boards;
using Avr8Sharp.TestKit;

namespace PyMCU.IntegrationTests.Tests.AVR;

/// <summary>
/// adafruit_neopixel's wheel() shape: `return (r, g, b) if ORDER in {RGB, GRB}
/// else (r, g, b, 0)` -- a tuple that lives in a conditional expression's arm,
/// selected by a compile-time string membership over a module constant. The
/// caller `s[i] = wheel(pos)` binds the delivered slots to `__setitem__`'s `val`
/// sequence, so the whole chain carries a run-time pos into the strip's buffer.
/// </summary>
[TestFixture]
public class CondTupleReturnTests
{
    /// <summary>
    /// wheel(GPIOR0.value) returns (0, 2, 3) once the membership folds true; the
    /// buffer holds r, g, b in order, so the output is only right if the slots
    /// survived _parse_color-style unpack and the __setitem__ dispatch.
    /// </summary>
    [Test]
    public void AConditionalTupleReturn_DeliversTheTakenArmThroughSetitem()
    {
        var uno = new SimSession(
            PymcuCompiler.BuildFixture("cond-tuple-return")).Reset();
        uno.RunUntilSerial(uno.Serial, "done\n", maxMs: 400);

        uno.Serial.Text.Should().Contain("0\n2\n3\ndone\n");
    }
}
