// SPDX-License-Identifier: MIT
using FluentAssertions;
using NUnit.Framework;
using Avr8Sharp.TestKit.Boards;
using Avr8Sharp.TestKit;

namespace PyMCU.IntegrationTests.Tests.AVR;

/// <summary>
/// One module per device, with the pin objects at the top and the functions that drive them
/// below, is the ordinary way to split a program across files. PyMCU/PyMCU#117 tracked this
/// arrangement failing to build, through four different messages as the import and lowering
/// work landed.
///
/// It closed as no defect: the fixture asked for a name, `OUTPUT`, that pymcu.hal.gpio does
/// not export and never did, so it was never valid PyMCU. The messages before the last one
/// were that nonexistent name surfacing several steps away from the import that should have
/// rejected it; the import diagnostics that landed report it where it is written.
///
/// The fixture is kept with the supported spelling, because the arrangement itself is worth
/// holding: a peripheral built at module level in an imported module, driven from a function
/// in that same module, called from the entry file.
/// </summary>
[TestFixture]
public class ImportedModulePinTests
{
    /// <summary>
    /// END is the discriminating half. IMP alone prints before blink() runs, so a build that
    /// reached the import and then failed on the pin would still show it.
    /// </summary>
    [Test]
    public void APinBuiltAtModuleLevelInAnImportedModuleDrivesFromThere()
    {
        var uno = new SimSession(PymcuCompiler.BuildFixture("imported-module-pin")).Reset();
        uno.RunUntilSerial(uno.Serial, "END\n", maxMs: 300);

        uno.Serial.Text.Should().Contain("IMP\nEND\n");
    }
}
