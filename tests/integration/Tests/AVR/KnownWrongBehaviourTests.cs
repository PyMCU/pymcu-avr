// SPDX-License-Identifier: MIT
using System;
using FluentAssertions;
using NUnit.Framework;
using Avr8Sharp.TestKit.Boards;
using Avr8Sharp.TestKit;

namespace PyMCU.IntegrationTests.Tests.AVR;

/// <summary>
/// Two open bugs, each pinned by what the compiler does TODAY rather than by what it
/// should do, so the suite stays green while they stand and turns red the moment either
/// is fixed. Same contract as <see cref="Differential.DifferentialCorpus.KnownDivergences"/>:
/// the entry is the record, and it invalidates itself. When one of these fails, the fix
/// landed -- read the issue, swap the assertion for the correct-behaviour one written
/// beside it, and delete the rest.
/// </summary>
[TestFixture]
public class KnownWrongBehaviourTests
{
    /// <summary>
    /// PyMCU/PyMCU#116. An object a coroutine builds for itself is not kept in the
    /// coroutine's state, so two instances of the same coroutine share one copy of it.
    ///
    /// worker(10) and worker(20) each build their own Acc and add 1 to it, so CPython
    /// reports 11 then 21. PyMCU reports 21 then 22: both instances construct in their
    /// first state before either awaits, the second construction overwrites the first,
    /// and from then on both add to the same object.
    ///
    /// WHEN THIS FAILS: expect "TLO\n11\n21\nEND\n" instead.
    /// </summary>
    [Test]
    public void Issue116_TwoInstancesOfACoroutineShareAnObjectBuiltInsideIt()
    {
        var uno = new SimSession(PymcuCompiler.BuildFixture("async-task-local-object")).Reset();
        uno.RunUntilSerial(uno.Serial, "END\n", maxMs: 500);

        uno.Serial.Text.Should().Contain("TLO\n21\n22\nEND\n",
            "#116 is open: the two coroutines share one Acc, so they report 21 and 22 where "
            + "CPython reports 11 and 21. If this assertion failed, check whether the output "
            + "is now TLO/11/21/END, and if so close #116 and assert that instead.");
    }

    /// <summary>
    /// PyMCU/PyMCU#117. A peripheral declared at module level in an IMPORTED module does
    /// not build. The same declaration in the entry file is fine, and so is a plain user
    /// class in the same position, so it takes both halves.
    ///
    /// The failure has MOVED TWICE, and each move is progress rather than a new bug:
    ///
    ///   1. originally: a bit-indexing operation the program does not perform, at a line the
    ///      file does not have
    ///   2. after PyMCU accc7aab (an imported module runs its own module level): the same
    ///      message, but on the field READ rather than on a construction that never happened
    ///   3. after the import work that hoists __module_init ahead of the functions that read
    ///      what it binds: "Parameter 'mode' is declared as const[uint8] and requires a
    ///      compile-time constant". The Pin is constructed now; what does not resolve is
    ///      OUTPUT, a constant IMPORTED INTO the imported module, which does not survive as a
    ///      compile-time constant across that second hop.
    ///
    /// So what is left under #117 is a different defect from the one it was filed for, and the
    /// message is at least about something the program contains.
    ///
    /// WHEN THIS FAILS: the build succeeded. Close #117 and replace this with a run that
    /// expects "IMP\nEND\n" on the serial line.
    /// </summary>
    [Test]
    public void Issue117_APinAtModuleLevelInAnImportedModuleDoesNotBuild()
    {
        var build = () => PymcuCompiler.BuildFixture("imported-module-pin");

        build.Should().Throw<InvalidOperationException>(
                "#117 is open: a Pin declared at module level in an imported module fails to "
                + "build. If no exception was thrown, the fix landed -- close #117 and turn "
                + "this into a run that expects IMP/END.")
            .WithMessage("*is declared as const[uint8] and requires a compile-time*");
    }
}
