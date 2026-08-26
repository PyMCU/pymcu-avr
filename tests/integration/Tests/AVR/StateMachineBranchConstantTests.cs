// SPDX-License-Identifier: MIT
using FluentAssertions;
using NUnit.Framework;
using Avr8Sharp.TestKit.Boards;
using Avr8Sharp.TestKit;

namespace PyMCU.IntegrationTests.Tests.AVR;

/// <summary>
/// PyMCU/PyMCU#108, pinned by what the compiler does TODAY so the suite stays green while it
/// stands and turns red the moment it is fixed. Same contract as
/// <see cref="Differential.DifferentialCorpus.KnownDivergences"/>: the entry is the record and
/// it invalidates itself.
///
/// A field a branch assigns a constant to is still believed when a SIBLING branch reads it, so
/// `self._n = self._n + 1` in the second arm folds into a store of the constant 1 and the field
/// never accumulates. It needs the `for` in arm 0 as well, which is what makes poll() flatten
/// into the caller rather than be outlined.
///
/// There is no async here. It is the shape the coroutine desugar emits for every `await`, which
/// is why an `async def` with an await-free `for` loses everything after its first await.
/// </summary>
[TestFixture]
public class StateMachineBranchConstantTests
{
    /// <summary>
    /// WHEN THIS FAILS: the fix landed. Expect "F\n1\n2\n3\nZ\n" and close #108.
    /// </summary>
    [Test]
    public void Issue108_AConstantAssignedInOneArmIsBelievedWhileASiblingArmReadsIt()
    {
        var uno = new SimSession(
            PymcuCompiler.BuildFixture("state-machine-branch-constant")).Reset();
        try { uno.RunUntilSerial(uno.Serial, "Z\n", maxMs: 200); } catch { /* it never gets there */ }

        // The discriminating assertion: the field reports 1 twice running. Correct behaviour
        // prints 1 then 2. Asserting only that "Z" is missing would also pass for a program
        // that crashed, which is a different bug.
        uno.Serial.Text.Should().Contain("F\n1\n1\n",
            "#108 is open: the increment folds to a store of the constant 1, so the field never "
            + "accumulates and the machine stays in its second arm. If this failed, check "
            + "whether the output is now F/1/2/3/Z, and if so close #108 and assert that.");
    }
}
