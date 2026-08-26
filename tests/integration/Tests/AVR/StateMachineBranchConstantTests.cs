// SPDX-License-Identifier: MIT
using FluentAssertions;
using NUnit.Framework;
using Avr8Sharp.TestKit.Boards;
using Avr8Sharp.TestKit;

namespace PyMCU.IntegrationTests.Tests.AVR;

/// <summary>
/// PyMCU/PyMCU#108 and #122, which turned out to be one bug: a constant a branch assigned was
/// still believed while a SIBLING branch read it, so the read folded. In a state machine the
/// second arm's `self._n = self._n + 1` became a store of the constant 1, the field never
/// accumulated, and the machine could not leave that arm.
///
/// The first fixture has no async in it at all, which is the point: it is the shape the
/// coroutine desugar emits for every `await`. It needs the `for` in arm 0 only because that is
/// what makes poll() flatten into the caller rather than be outlined, and a flattened field is
/// a plain name the constant folder tracks.
/// </summary>
[TestFixture]
public class StateMachineBranchConstantTests
{
    /// <summary>
    /// The discriminating assertion is 1 then 2: the field has to ACCUMULATE. Asserting only
    /// that "Z" arrives would also pass for a machine that skipped its second arm entirely.
    /// </summary>
    [Test]
    public void Issue108_AFieldAssignedInOneArmAccumulatesWhenAnotherArmIncrementsIt()
    {
        var uno = new SimSession(
            PymcuCompiler.BuildFixture("state-machine-branch-constant")).Reset();
        uno.RunUntilSerial(uno.Serial, "Z\n", maxMs: 300);

        uno.Serial.Text.Should().Contain("F\n1\n2\n3\nZ\n");
    }

    /// <summary>
    /// #122. The discriminating parts are `99`, which prints only if the await completed, and
    /// the trailing `1`, which is the global read back after run() returns: a program that
    /// woke but lost the write would print 0 there.
    /// </summary>
    [Test]
    public void Issue122_ACoroutineThatAssignsAGlobalWakesFromItsNextAwait()
    {
        var uno = new SimSession(
            PymcuCompiler.BuildFixture("async-global-then-await")).Reset();
        uno.RunUntilSerial(uno.Serial, "Z\n", maxMs: 300);

        uno.Serial.Text.Should().Contain("CT\n98\n99\n1\nZ\n");
    }
}
