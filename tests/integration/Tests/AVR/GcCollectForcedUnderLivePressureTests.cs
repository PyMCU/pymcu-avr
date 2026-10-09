// SPDX-License-Identifier: MIT
using FluentAssertions;
using NUnit.Framework;
using Avr8Sharp.TestKit.Boards;

namespace PyMCU.IntegrationTests.Tests.AVR;

/// <summary>
/// Forces a real mark-and-compact collection (not just an allocation that happens to
/// succeed without ever needing one) while several live, aliased objects -- flat and
/// nested (ref-bearing) -- are reachable only through the shadow stack and, for the
/// inner lists, only through the ref-tracing pass gc_runtime.S's _gc_trace_refs adds.
/// Every element and every alias is checked against CPython afterward, and a collection
/// having actually run is confirmed by sampling the bump allocator's own write pointer
/// (_gc_heap_top, already in gc_runtime.S for gc_alloc's own use) while the garbage
/// loop runs: no counter or other new runtime code is needed -- a real compaction makes
/// that pointer drop, which a lazy allocator that only grows (and would have failed
/// outright, long before reaching this test's assertions) cannot otherwise produce.
///
/// The garbage loop's size (300 iterations of a 100-byte throwaway list) is tuned for
/// ATmega328P's ~1.7-1.9 KiB usable heap: cumulatively 30 KB if nothing were ever
/// reclaimed, comfortably forcing more than a dozen collections. See
/// Rp2040/GcCollectForcedUnderLivePressureTests.cs for the same scenario on ARM, tuned
/// for that target's much larger heap instead -- same live-object structure, different
/// stress parameters; an identical byte count would either never collect on ARM or
/// take unreasonably long to simulate on AVR.
/// </summary>
[TestFixture]
public class GcCollectForcedUnderLivePressureTests
{
    // Oracle-probe-style source: a flat list, a nested (list of lists) one with two
    // separate aliases (an inner-list alias and a whole-list alias), a function that
    // reads through a list parameter, then a garbage loop sized to force a real
    // collection, then one more live object allocated after the garbage.
    private const string Source =
        "from pymcu.types import uint8, uint16\n\n" +
        "flat: list[uint8] = []\n" +
        "flat.append(10)\n" +
        "flat.append(20)\n" +
        "flat.append(30)\n\n" +
        "nested: list[list[uint8]] = [[1, 2], [3, 4]]\n" +
        "alias_a = nested[0]\n" +
        "alias_b = nested\n\n\n" +
        "def read_flat(xs: list[uint8]) -> uint8:\n" +
        "    return xs[1]\n\n\n" +
        "i: uint16 = 0\n" +
        "while i < 300:\n" +
        "    junk: list[uint8] = [0] * 100\n" +
        "    i = i + 1\n\n" +
        "tail: list[uint8] = []\n" +
        "tail.append(99)\n\n" +
        "print(flat[0], flat[1], flat[2])\n" +
        "print(nested[0][0], nested[0][1], nested[1][0], nested[1][1])\n" +
        "print(alias_a[0], alias_a[1])\n" +
        "alias_b[1][1] = 77\n" +
        "print(nested[1][1])\n" +
        "print(read_flat(flat))\n" +
        "print(tail[0])\n" +
        "print(\"END\")\n" +
        "while True:\n" +
        "    pass\n";

    // _gc_heap_top_lo/hi and _heap_start: this exact program's build places them at
    // SRAM 0x013D/0x013E and 0x0143 (RAMSTART 0x0100 + offsets 61/62/67 -- see
    // EmitGcSramLayout's layout comment in AvrCodeGen.cs; re-derive from a fresh
    // dist/firmware.gas.asm's ".equ _gc_heap_top_lo"/".equ _heap_start" lines if this
    // source text ever changes, since the offsets depend on the program's own
    // static-storage footprint).
    private const int HeapTopLo = 0x013D;
    private const int HeapTopHi = 0x013E;

    [Test]
    public void FlatAndNestedListsSurviveARealCollection_AndTheHeapPointerProvesItRan()
    {
        var uno = new ArduinoUnoSimulation();
        uno.WithHex(PymcuCompiler.BuildSource(Source));

        // Sample the bump pointer every 0.1 ms of simulated time while the garbage loop
        // runs. Measured directly for this exact program: the loop (300 x 100 B against
        // a ~1.9 KB heap) finishes within about 3 ms of simulated time at 16 MHz, during
        // which the pointer rises (ordinary bump allocation), then drops sharply more
        // than once (a real mark-and-compact collection reclaiming the loop's dead
        // objects) before rising again. 10 ms is a generous multiple of that, so a
        // future change to the codegen or the loop shifting the exact timing still
        // samples through the whole garbage phase.
        var samples = new System.Collections.Generic.List<int>();
        for (int step = 0; step < 100; step++)
        {
            uno.RunMilliseconds(0.1);
            samples.Add(uno.Memory[HeapTopLo] | (uno.Memory[HeapTopHi] << 8));
        }

        // A lazy bump allocator only grows between collections -- it falls back to
        // gc_collect() exactly when it cannot otherwise satisfy a request, and nothing
        // else in this runtime ever moves heap_top backward. Finding ANY two
        // consecutive samples where it drops by more than a single allocation's worth
        // of bytes (100 B here) is therefore only possible if a real compaction ran
        // between them and reclaimed the loop's garbage -- not an allocation that
        // merely happened to succeed without ever needing one.
        int biggestDrop = 0;
        for (int k = 1; k < samples.Count; k++)
            biggestDrop = Math.Max(biggestDrop, samples[k - 1] - samples[k]);
        biggestDrop.Should().BeGreaterThan(100,
            "the garbage loop (300 x 100 B, cumulatively 30 KB against a ~1.7-1.9 KB "
            + "heap) must have forced gc_alloc to collect at least once -- sampled over "
            + $"{samples.Count} steps of 0.1 ms, the heap pointer never dropped by more "
            + "than a single allocation's worth, which means the stress never actually "
            + "exercised a real compaction");

        uno.RunUntilSerial(uno.Serial, "END\n", maxMs: 8000);

        var text = uno.Serial.Text.Replace("\r", "");
        // Verified directly against CPython running the same live-object sequence
        // (the garbage loop has no observable effect on it): 10 20 30; nested unwraps
        // to 1 2 3 4; alias_a (== nested[0]) reads 1 2; alias_b[1][1] = 77 is visible
        // through nested itself; read_flat(flat) is flat[1] == 20; tail[0] == 99.
        text.Should().Be("10 20 30\n1 2 3 4\n1 2\n77\n20\n99\nEND\n",
            "every live object -- flat, nested, both its aliases, and the post-garbage "
            + "tail -- must read back correctly after a real compaction");
    }
}
