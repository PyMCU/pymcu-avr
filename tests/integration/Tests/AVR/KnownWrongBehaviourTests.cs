// SPDX-License-Identifier: MIT
using System;
using FluentAssertions;
using NUnit.Framework;
using Avr8Sharp.TestKit.Boards;
using Avr8Sharp.TestKit;

namespace PyMCU.IntegrationTests.Tests.AVR;

/// <summary>
/// An open bug, pinned by what the compiler does TODAY rather than by what it should do, so
/// the suite stays green while it stands and turns red the moment it is fixed. Same contract as <see cref="Differential.DifferentialCorpus.KnownDivergences"/>:
/// the entry is the record, and it invalidates itself. When this fails, the fix landed -- read the issue, swap the assertion for the correct-behaviour one written
/// beside it, and delete the rest.
/// </summary>
[TestFixture]
public class KnownWrongBehaviourTests
{
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
    ///      compile-time constant"
    ///   4. after PyMCU#158, at the import itself and naming the real problem: "cannot import
    ///      'OUTPUT' from 'pymcu.hal.gpio'". OUTPUT does not exist anywhere in the stdlib, so
    ///      this fixture was never valid Python against it. Every message before this one was
    ///      a nonexistent name surfacing several steps from the import that should have
    ///      rejected it.
    ///
    /// So what is left under #117 is not a defect at all in this fixture: it is the fixture
    /// asking for a name that is not there, now reported where it is written. The supported
    /// spelling is Pin("PD5", Pin.OUT).
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
            .WithMessage("*cannot import 'OUTPUT' from 'pymcu.hal.gpio'*");
    }
}
