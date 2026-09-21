// SPDX-License-Identifier: MIT
using FluentAssertions;
using NUnit.Framework;
using Avr8Sharp.TestKit.Boards;
using Avr8Sharp.TestKit;

namespace PyMCU.IntegrationTests.Tests.AVR;

/// <summary>
/// `seq.index(x)` on a module-level constant tuple -- adafruit_tcs34725's
/// `_GAINS.index(val)` over `_GAINS = (1, 4, 16, 60)`. A constant needle folds,
/// a run-time needle lowers to a first-match compare chain, and a miss raises
/// ValueError that a try/except catches.
/// </summary>
[TestFixture]
public class ConstSeqIndexTests
{
    [Test]
    public void IndexOnAConstTuple_FoldsChainsAndRaises()
    {
        var uno = new SimSession(
            PymcuCompiler.BuildFixture("const-seq-index")).Reset();
        uno.RunUntilSerial(uno.Serial, "done\n", maxMs: 400);

        uno.Serial.Text.Should().Contain("2\n2\nVE\ndone\n");
    }
}
