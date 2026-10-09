// SPDX-License-Identifier: MIT
using FluentAssertions;
using NUnit.Framework;
using Avr8Sharp.TestKit.Boards;

namespace PyMCU.IntegrationTests.Tests.AVR;

/// <summary>
/// Of the 9 GcAlloc call sites in IRGenerator, 7 already raised MemoryError on a null (OOM)
/// result before writing anything through the returned pointer; two did not -- an annotated
/// `x: list[T] = [...]` declaration with a literal initializer (EmitListAnnAssign, Assign.cs)
/// and `x = []` later appended (promotedEmptyLists, Assign.cs). Both now emit the same check
/// the other 7 use.
///
/// R1 is the AVR "always zero" register this backend's own generated code relies on for
/// every CPC/SBC-based 16-bit comparison (AvrCodeGen.cs: "R1 is the AVR zero register and is
/// always 0 in PyMCU-generated code"). Before the fix, the unchecked header store on a null
/// result wrote count=0 to SRAM address 0x0000 (R0) and the low byte of capacity to 0x0001
/// (R1) -- for `list(300)`, capacity's low byte is 0x2C, corrupting R1 permanently and
/// silently, with no exception, no crash, no diagnostic.
/// </summary>
[TestFixture]
public class GcAllocNullCheckTests
{
    private static ArduinoUnoSimulation Run(string mainPy, out ArduinoUnoSimulation uno)
    {
        uno = new ArduinoUnoSimulation();
        uno.WithHex(PymcuCompiler.BuildSource(mainPy));
        return uno;
    }

    [Test]
    public void AnnotatedListLiteralExceedingTheObjectCeiling_RaisesMemoryError_AndR1StaysZero()
    {
        // EmitListAnnAssign's call site: a 254-element literal asks for a 256-byte object
        // (2-byte header + 254 payload) -- gc_alloc's own 255-byte size check refuses this
        // outright, deterministically, with no need to exhaust the heap's free space first
        // (oracle probe 840).
        var elements = string.Join(", ", System.Linq.Enumerable.Range(0, 254).Select(i => i % 250));
        var src =
            "from pymcu.types import uint8\n\n" +
            "try:\n" +
            $"    x: list[uint8] = [{elements}]\n" +
            "    print(len(x))\n" +
            "except MemoryError:\n" +
            "    print(\"MemoryError\")\n" +
            "print(\"END\")\n";

        var uno = Run(src, out var sim);
        sim.RunUntilSerial(sim.Serial, "END\n", maxMs: 3000);
        var text = sim.Serial.Text.Replace("\r", "");

        text.Should().Be("MemoryError\nEND\n",
            "a doubly-failed allocation must raise, not write a list header through a null pointer");
        sim.Memory[1].Should().Be(0x00,
            "R1 is the AVR zero register every 16-bit comparison in this backend's own "
            + "generated code relies on -- it must survive a failed allocation untouched");
    }

    [Test]
    public void HeapGenuinelyExhaustedByStaticStorage_RaisesMemoryError_AndR1StaysZero()
    {
        // promotedEmptyLists' call site (`x = []` later appended): its own allocation is a
        // fixed 2 bytes, too small for gc_alloc's size ceiling to ever refuse outright, so
        // this needs the heap genuinely exhausted instead. A module-level array sized to use
        // nearly all of an ATmega328P's 2048 bytes of SRAM leaves only a few bytes of heap
        // for the rest of this exact program (oracle probe 841) -- measured directly to land
        // on `y.append(1)`'s own (already-checked, pre-existing) grow path for this exact
        // program shape rather than `y = []`'s bare promotion, which the IR-level
        // EmptyListPromotionChecksTheAllocationResult unit test (PyMCU, ListGrowCeilingTests)
        // pins directly instead: this chip's own static-storage ceiling (AvrCodeGen.cs,
        // "needs 64 bytes for the call stack") always leaves at least ~16 bytes of heap, more
        // than the 2 bytes that bare allocation needs. Either way, the end-to-end behaviour
        // this test demonstrates -- genuine exhaustion raises MemoryError, not corruption --
        // is the same fix class.
        var src =
            "from pymcu.types import uint8\n\n" +
            "pad: uint8[1957] = [0] * 1957\n\n" +
            "try:\n" +
            "    y = []\n" +
            "    y.append(1)\n" +
            "    print(len(y))\n" +
            "except MemoryError:\n" +
            "    print(\"MemoryError\")\n" +
            "print(\"END\")\n";

        var uno = Run(src, out var sim);
        sim.RunUntilSerial(sim.Serial, "END\n", maxMs: 3000);
        var text = sim.Serial.Text.Replace("\r", "");

        text.Should().Be("MemoryError\nEND\n",
            "a genuinely exhausted heap must raise, not write a list header through a null pointer");
        sim.Memory[1].Should().Be(0x00,
            "R1 is the AVR zero register every 16-bit comparison in this backend's own "
            + "generated code relies on -- it must survive a failed allocation untouched");
    }
}
