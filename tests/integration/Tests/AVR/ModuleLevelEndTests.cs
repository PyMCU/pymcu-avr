// SPDX-License-Identifier: MIT
using FluentAssertions;
using NUnit.Framework;
using Avr8Sharp.TestKit.Boards;
using Avr8Sharp.TestKit;

namespace PyMCU.IntegrationTests.Tests.AVR;

/// <summary>
/// fixtures/module-level-end, fixtures/def-main-end: a program whose module level
/// can reach its end parks the CPU instead of returning into nothing.
///
/// The entry function is reached by RJMP with an empty hardware stack, so the
/// RET its trailing Return used to lower to popped two bytes from unimplemented
/// SRAM past RAMEND (SP ended at 0x0901, one word too high) and jumped wherever
/// they pointed -- on silicon a reboot loop or a wild PC, in the simulator an
/// IndexOutOfRangeException from the data-space read. Every reachable Return in
/// main now lowers to a jump to __pymcu_halt (cli + rjmp .-2), the avr-libc
/// _exit idiom: interrupts off, last output state held, SP untouched.
///
/// fixtures/module-level-loop is the other half of the contract: a body that
/// cannot fall through has its appended Return deleted by the CFG pass, so its
/// image must not contain __pymcu_halt at all.
/// </summary>
[TestFixture]
public class ModuleLevelEndTests
{
    private SimSession _moduleEnd = null!;
    private SimSession _defMainEnd = null!;

    [OneTimeSetUp]
    public void BuildFirmware()
    {
        _moduleEnd  = new SimSession(PymcuCompiler.BuildFixture("module-level-end"));
        _defMainEnd = new SimSession(PymcuCompiler.BuildFixture("def-main-end"));
    }

    [Test]
    public void ModuleLevelEnd_ParksTheCpu()
    {
        var uno = _moduleEnd.Reset();
        uno.RunMilliseconds(10);
        var pc = uno.Cpu.Pc;
        uno.RunMilliseconds(10);
        uno.Cpu.Pc.Should().Be(pc,
            "the halt is a tight loop with interrupts off -- the CPU is parked, not restarted");
    }

    [Test]
    public void ModuleLevelEnd_KeepsTheLastOutputState()
    {
        var uno = _moduleEnd.Reset();
        uno.RunMilliseconds(10);
        uno.PortB.Should().HavePinHigh(5,
            "PB5 was driven high before the module level ended and the park holds it");
    }

    [Test]
    public void ModuleLevelEnd_LeavesTheStackPointerAtRamend()
    {
        var uno = _moduleEnd.Reset();
        uno.RunMilliseconds(10);
        uno.Cpu.Sp.Should().Be(0x08FF,
            "no RET ran on an empty stack, so SP never moved past RAMEND");
    }

    [Test]
    public void DefMainEnd_ParksTheCpu()
    {
        var uno = _defMainEnd.Reset();
        uno.RunMilliseconds(10);
        var pc = uno.Cpu.Pc;
        uno.RunMilliseconds(10);
        uno.Cpu.Pc.Should().Be(pc,
            "def main() produces the same entry shape, so its end parks identically");
    }

    [Test]
    public void DefMainEnd_KeepsTheLastOutputState()
    {
        var uno = _defMainEnd.Reset();
        uno.RunMilliseconds(10);
        uno.PortB.Should().HavePinHigh(5);
    }

    [Test]
    public void DefMainEnd_LeavesTheStackPointerAtRamend()
    {
        var uno = _defMainEnd.Reset();
        uno.RunMilliseconds(10);
        uno.Cpu.Sp.Should().Be(0x08FF);
    }

    [Test]
    public void ModuleLevelLoop_EmitsNoHaltBlock()
    {
        PymcuCompiler.BuildFixture("module-level-loop");
        var asm = File.ReadAllText(Path.Combine(
            PymcuCompiler.FixtureDir("module-level-loop"), "dist", "debug", "firmware.asm"));
        asm.Should().NotContain("__pymcu_halt",
            "a body that cannot fall through has its appended Return deleted as unreachable, " +
            "so a never-ending program pays nothing for the park");
    }
}
