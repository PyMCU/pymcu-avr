// SPDX-License-Identifier: MIT
// PyMCU AVR Backend — GPIOR promotion for ISR-shared globals.

using PyMCU.Common.Models;
using PyMCU.IR;

namespace PyMCU.Backend.Targets.AVR;

/// <summary>
/// Promotes single-byte ISR-shared globals (ProgramIR.IsrSharedGlobals, computed
/// by the core optimizer) from SRAM to the chip's general-purpose I/O registers
/// GPIOR0..GPIOR2. A promoted global is rewritten in place: every Variable
/// reference becomes a MemoryAddress at the GPIOR's data-space address, so the
/// existing codegen emits IN/OUT (1 cycle, always volatile) instead of LDS/STS,
/// and SBI/CBI/SBIS/SBIC for bit operations on GPIOR0 — exactly the access
/// pattern an expert writes by hand for ISR↔main flags.
///
/// Safety rules:
///  - Only UINT8 globals are eligible (MemoryAddress of size 1 is typed UINT8
///    by the codegen, so promoting an INT8 would lose signedness in compares).
///  - A global is skipped if it appears in an InlineAsm operand list, inside a
///    naked asm string by mangled name, or in a VirtualCall / GcRoot / GcUnroot
///    (those paths resolve names by SRAM address or register convention).
///  - A GPIOR already referenced explicitly by the program (e.g. user code that
///    imports GPIOR0 from pymcu.chips.*) is never reassigned.
///  - Chips whose GPIOR layout is not in the table get no promotion at all —
///    the globals simply stay in SRAM, which is always correct.
/// </summary>
public static class AvrGpiorPromotion
{
    /// <summary>
    /// Data-space addresses of GPIOR0..GPIOR2, ordered so the bit-addressable
    /// register (SBI/CBI reachable, data address ≤ 0x3F) comes first.
    /// </summary>
    private static int[]? GpiorAddressesFor(string chip)
    {
        if (string.IsNullOrEmpty(chip)) return null;
        var c = chip.ToLowerInvariant();

        // Classic megaAVR layout: GPIOR0 = 0x3E (I/O 0x1E, bit-addressable),
        // GPIOR1 = 0x4A, GPIOR2 = 0x4B.
        string[] mega =
        [
            "atmega328", "atmega168", "atmega88", "atmega48",
            "atmega2560", "atmega1280", "atmega640",
            "atmega32u4", "atmega16u4",
            "atmega1284", "atmega644", "atmega324", "atmega164",
        ];
        if (mega.Any(c.StartsWith))
            return [0x3E, 0x4A, 0x4B];

        // tinyAVR (25/45/85 family): GPIOR0 = 0x31, GPIOR1 = 0x32, GPIOR2 = 0x33,
        // all three bit-addressable (I/O 0x11..0x13).
        string[] tiny = ["attiny25", "attiny45", "attiny85", "attiny24", "attiny44", "attiny84"];
        if (tiny.Any(c.StartsWith))
            return [0x31, 0x32, 0x33];

        return null;
    }

    /// <summary>
    /// Mutates <paramref name="program"/>: rewrites references to the promoted
    /// globals and removes them from Globals (they no longer occupy BSS).
    /// Returns the promotion map (global name → data-space address) for the
    /// assembly header comment; empty when nothing was promoted.
    /// </summary>
    public static Dictionary<string, int> Apply(ProgramIR program, DeviceConfig cfg)
    {
        var result = new Dictionary<string, int>();
        if (program.IsrSharedGlobals.Count == 0) return result;

        // The backend CLI populates TargetChip (source of truth); Chip is only
        // set on the in-process path. Accept either.
        var chip = !string.IsNullOrEmpty(cfg.Chip) ? cfg.Chip : cfg.TargetChip;
        var gpiors = GpiorAddressesFor(chip);
        if (gpiors == null) return result;

        var eligible = program.Globals
            .Where(g => g.Type == DataType.UINT8 && program.IsrSharedGlobals.Contains(g.Name))
            .Select(g => g.Name)
            .ToHashSet();
        if (eligible.Count == 0) return result;

        // Exclusions + explicit-GPIOR conflict scan + use counting, single walk.
        var usedAddresses = new HashSet<int>();
        var useCount = eligible.ToDictionary(n => n, _ => 0);
        foreach (var instr in program.Functions.SelectMany(f => f.Body))
        {
            switch (instr)
            {
                case InlineAsm { Operands: not null } ia:
                    foreach (var op in ia.Operands)
                        if (op is Variable v) eligible.Remove(v.Name);
                    break;
                case InlineAsm { Code: var asmStr }:
                    foreach (var n in eligible.Where(n => asmStr.Contains(n.Replace('.', '_'))).ToList())
                        eligible.Remove(n);
                    break;
                case VirtualCall vc:
                    eligible.Remove(vc.Self.Name);
                    foreach (var a in vc.Args)
                        if (a is Variable av) eligible.Remove(av.Name);
                    break;
                case GcRoot { Var: Variable grv }: eligible.Remove(grv.Name); break;
                case GcUnroot { Var: Variable guv }: eligible.Remove(guv.Name); break;
            }

            VisitVals(instr, val =>
            {
                if (val is MemoryAddress mem) usedAddresses.Add(mem.Address);
                if (val is Variable v && useCount.ContainsKey(v.Name)) useCount[v.Name]++;
            });
        }

        var freeGpiors = gpiors.Where(a => !usedAddresses.Contains(a)).ToList();
        if (freeGpiors.Count == 0) return result;

        // Most-used global first: it gets GPIOR0, the SBI/CBI-reachable register.
        var winners = useCount
            .Where(kv => eligible.Contains(kv.Key) && kv.Value > 0)
            .OrderByDescending(kv => kv.Value)
            .ThenBy(kv => kv.Key, StringComparer.Ordinal)
            .Take(freeGpiors.Count)
            .Select((kv, i) => (Name: kv.Key, Address: freeGpiors[i]))
            .ToList();
        if (winners.Count == 0) return result;

        foreach (var (name, address) in winners)
            result[name] = address;

        Val Sub(Val val) =>
            val is Variable v && result.TryGetValue(v.Name, out var addr)
                ? new MemoryAddress(addr, v.Type)
                : val;

        foreach (var func in program.Functions)
            for (int i = 0; i < func.Body.Count; i++)
                func.Body[i] = RewriteVals(func.Body[i], Sub);

        program.Globals.RemoveAll(g => result.ContainsKey(g.Name));
        return result;
    }

    private static void VisitVals(Instruction instr, Action<Val> visit)
    {
        // Visiting Dst positions too: a write is a reference for both the
        // use counter and the conflict scan.
        RewriteVals(instr, v => { visit(v); return v; });
    }

    /// <summary>Rewrites every Val operand (sources and destinations) of an instruction.</summary>
    private static Instruction RewriteVals(Instruction instr, Func<Val, Val> f) => instr switch
    {
        Return r => r with { Value = f(r.Value) },
        Unary u => u with { Src = f(u.Src), Dst = f(u.Dst) },
        Binary b => b with { Src1 = f(b.Src1), Src2 = f(b.Src2), Dst = f(b.Dst) },
        Copy c => c with { Src = f(c.Src), Dst = f(c.Dst) },
        Bitcast bc => bc with { Src = f(bc.Src), Dst = f(bc.Dst) },
        LoadIndirect li => li with { SrcPtr = f(li.SrcPtr), Dst = f(li.Dst) },
        StoreIndirect si => si with { Src = f(si.Src), DstPtr = f(si.DstPtr) },
        JumpIfZero j => j with { Condition = f(j.Condition) },
        JumpIfNotZero j => j with { Condition = f(j.Condition) },
        JumpIfEqual j => j with { Src1 = f(j.Src1), Src2 = f(j.Src2) },
        JumpIfNotEqual j => j with { Src1 = f(j.Src1), Src2 = f(j.Src2) },
        JumpIfLessThan j => j with { Src1 = f(j.Src1), Src2 = f(j.Src2) },
        JumpIfLessOrEqual j => j with { Src1 = f(j.Src1), Src2 = f(j.Src2) },
        JumpIfGreaterThan j => j with { Src1 = f(j.Src1), Src2 = f(j.Src2) },
        JumpIfGreaterOrEqual j => j with { Src1 = f(j.Src1), Src2 = f(j.Src2) },
        Call cl => cl with { Args = cl.Args.Select(f).ToList(), Dst = f(cl.Dst) },
        IndirectCall ic => ic with { FuncAddr = f(ic.FuncAddr), Args = ic.Args.Select(f).ToList(), Dst = f(ic.Dst) },
        BitSet bs => bs with { Target = f(bs.Target) },
        BitClear bc => bc with { Target = f(bc.Target) },
        BitCheck bck => bck with { Source = f(bck.Source), Dst = f(bck.Dst) },
        BitWrite bw => bw with { Target = f(bw.Target), Src = f(bw.Src) },
        JumpIfBitSet j => j with { Source = f(j.Source) },
        JumpIfBitClear j => j with { Source = f(j.Source) },
        AugAssign aa => aa with { Target = f(aa.Target), Operand = f(aa.Operand) },
        ArrayLoad al => al with { Index = f(al.Index), Dst = f(al.Dst) },
        ArrayStore ast => ast with { Index = f(ast.Index), Src = f(ast.Src) },
        ArrayLoadFlash alf => alf with { Index = f(alf.Index), Dst = f(alf.Dst) },
        FlashLoadPtr flp => flp with { Ptr = f(flp.Ptr), Index = f(flp.Index), Dst = f(flp.Dst) },
        BytearrayLoad bld => bld with { Index = f(bld.Index), Dst = f(bld.Dst) },
        BytearrayStore bst => bst with { Index = f(bst.Index), Src = f(bst.Src) },
        GcAlloc ga => ga with { Size = f(ga.Size), Dst = f(ga.Dst) },
        SignalError se => se with { Code = f(se.Code) },
        // InlineAsm / VirtualCall / GcRoot / GcUnroot: operands resolve by name,
        // register convention, or SRAM address — globals used there are excluded
        // from promotion in Apply(), so the instructions pass through unchanged.
        _ => instr,
    };
}
