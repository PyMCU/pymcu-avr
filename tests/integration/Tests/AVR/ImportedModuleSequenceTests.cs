using Avr8Sharp.TestKit.Boards;
using FluentAssertions;
using NUnit.Framework;
using PyMCU.TestKit;

namespace PyMCU.IntegrationTests.Tests.AVR;

/// <summary>
/// A sequence an IMPORTED module binds at its top level, read through a name inside one of that
/// module's methods (adafruit_onewire/device.py: `_MATCH_ROM = b"\x55"`, read by `buf[i]` once it
/// has been forwarded to the bus). Two faults sat on this path and both were silent:
///
/// - the elements are slots of the module's ___module_init frame; a reader in another frame named
///   a slot nothing wrote once the init's store was dropped as dead, and the bus got 0x00;
/// - bus.py binds the same name to an int (`_MATCH_ROM = const(0x55)`), and the last rung of the
///   name ladder let that int answer for the owner's bytes, so `buf[i]` became a bit test on 85
///   and the bus got 0x01.
///
/// The fixture reproduces both with two modules that share the names `_K` and `_M`. Expected under
/// CPython: 86, 165, 60, 85.
/// </summary>
[TestFixture]
public class ImportedModuleSequenceTests
{
    [Test]
    public void TheOwnersBytesAreRead_NotAnotherModulesInt()
    {
        var session = new SimSession(PymcuCompiler.BuildFixture("imported-module-sequence"));
        var uno = session.Reset();
        uno.RunUntilSerial(uno.Serial, "85\n", maxMs: 2000);
        uno.Serial.Text.Should().Be("86\n165\n60\n85\n");
    }
}
