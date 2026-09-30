// SPDX-License-Identifier: MIT
using FluentAssertions;
using NUnit.Framework;
using Avr8Sharp.TestKit.Boards;
using Avr8Sharp.TestKit;
using PyMCU.TestKit;

namespace PyMCU.IntegrationTests.Tests.AVR;

/// <summary>
/// RFC 0013 (docs/rfcs/0013-memory-model.md, PyMCU-rfc13), phase 0c ("static
/// by exclusion"): the minimal synthetic fixture for the three-level pattern
/// the beta-1 blocker generalises -- a module-level instance whose
/// <c>__init__</c> constructs a SECOND instance directly (not a constructor
/// argument of its own construction), which in turn holds a THIRD instance by
/// parameter alias, whose own mutable field is read in a loop. See
/// fixtures/static-by-exclusion-nested-lock/src/main.py for the full account
/// and PyMCU-gemlife-lockinit for the real-silicon bug (busio.I2C._locked,
/// reached through adafruit_bus_device.I2CDevice under a module-level Seesaw
/// instance) this fixture stands in for.
///
/// Phase 0c's rule needs no proof this field's write-site is module-rooted,
/// and no proof two flattened paths name "the same" object: any home that is
/// not a parameter, local or temporary of some function is static and zero-
/// initialized at boot, whatever it is named. Both
/// <see cref="Boot_PrintsOneUnpoisoned"/> (avr8sharp's own state starts
/// zeroed) and <see cref="Boot_PrintsOneEvenWhenPoisoned"/> (R0-R31 and SRAM
/// filled with 0xFF before the first instruction) must print the same thing.
/// </summary>
[TestFixture]
public class StaticByExclusionNestedLockTests
{
    private static readonly string Hex =
        PymcuCompiler.BuildFixture("static-by-exclusion-nested-lock");

    private static ArduinoUnoSimulation Boot(byte? poison)
    {
        var uno = new ArduinoUnoSimulation();
        uno.WithHex(Hex);
        if (poison is byte p) ColdBootRealism.Poison(uno, p);
        return uno;
    }

    [Test]
    public void Boot_PrintsOneUnpoisoned()
    {
        var uno = Boot(poison: null);
        uno.RunUntilSerial(uno.Serial, "1\n", maxMs: 500);
        uno.Serial.Text.Should().Be("1\n",
            "the nested lock must acquire on the very first try under avr8sharp's own " +
            "zeroed cold-boot state, exactly as it does under CPython");
    }

    [Test]
    public void Boot_PrintsOneEvenWhenPoisoned()
    {
        var uno = Boot(poison: 0xFF);
        uno.RunUntilSerial(uno.Serial, "1\n", maxMs: 500);
        uno.Serial.Text.Should().Be("1\n",
            "RFC 0013 phase 0c ('static by exclusion'): owner.holder.target.locked -- " +
            "three hops under a module-level instance, reached only through a held " +
            "instance built inside its owner's own __init__ (not a constructor argument) " +
            "and a second held instance aliased from a parameter -- is not a parameter, " +
            "local or temporary of any function, so it is static and the AVR backend " +
            "zero-initializes its register/SRAM home at boot regardless of what a real " +
            "cold-silicon power-on (simulated here by poisoning R0-R31 and SRAM to 0xFF " +
            "before the first instruction) leaves there. Before this rule, this printed 0 " +
            "and, with an unbounded retry loop instead of a single acquire, would spin " +
            "forever -- the same class of bug as busio.I2C.try_lock() under a module-level " +
            "Seesaw instance (PyMCU-gemlife-lockinit).");
    }
}
