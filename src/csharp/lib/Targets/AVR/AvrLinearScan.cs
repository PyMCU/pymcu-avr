/*
 * -----------------------------------------------------------------------------
 * PyMCU Compiler (pymcuc)
 * Copyright (C) 2026 Ivan Montiel Cardona and the PyMCU Project Authors
 *
 * SPDX-License-Identifier: MIT
 *
 * -----------------------------------------------------------------------------
 * SAFETY WARNING / HIGH RISK ACTIVITIES:
 * THE SOFTWARE IS NOT DESIGNED, MANUFACTURED, OR INTENDED FOR USE IN HAZARDOUS
 * ENVIRONMENTS REQUIRING FAIL-SAFE PERFORMANCE, SUCH AS IN THE OPERATION OF
 * NUCLEAR FACILITIES, AIRCRAFT NAVIGATION OR COMMUNICATION SYSTEMS, AIR
 * TRAFFIC CONTROL, DIRECT LIFE SUPPORT MACHINES, OR WEAPONS SYSTEMS.
 * -----------------------------------------------------------------------------
 */

using PyMCU.IR;

namespace PyMCU.Backend.Targets.AVR;

public static class AvrLinearScan
{
    private class LiveInterval
    {
        public string Name = "";
        public DataType Type;
        public int Def;
        public int LastUse;
        public bool SpansCall;
    }

    // Instructions with no IR Call whose lowering still writes R16/R17. The 32-bit integer
    // division and modulo go through __div32/__mod32/__divs32/__mods32, which use R16 as
    // their loop counter without saving it (PyMCU/PyMCU#408 moved their scratch into the
    // caller-saved set), and the GC shadow-stack push/pop stage the depth in R16:R17. A temp
    // homed in the pair across one of them came back as the counter's last value:
    // `(s + 300) + 100000 // (s + 7)` printed 14541 instead of 14585. The 8/16-bit
    // divisions, __mul32 and the float routines leave R16:R17 intact, so they stay out.
    // Over-approximates the width test of AvrCodeGen.CompileBinary (it widens to Src1, this
    // looks at every operand), which can only spill a temp that did not need it.
    private static bool ClobbersTempPair(Instruction instr)
    {
        static bool Wide(Val v) => v switch
        {
            Temporary t => t.Type.SizeOf() == 4 && t.Type != DataType.FLOAT,
            Variable vv => vv.Type.SizeOf() == 4 && vv.Type != DataType.FLOAT,
            MemoryAddress m => m.Type.SizeOf() == 4 && m.Type != DataType.FLOAT,
            Constant c => c.Value is > 65535 or < -32768,
            _ => false,
        };
        static bool IsFloat(Val v) => v switch
        {
            Temporary t => t.Type == DataType.FLOAT,
            Variable vv => vv.Type == DataType.FLOAT,
            MemoryAddress m => m.Type == DataType.FLOAT,
            FloatConstant => true,
            _ => false,
        };
        static bool IsDivMod(BinaryOp op) => op is BinaryOp.Div or BinaryOp.FloorDiv or BinaryOp.Mod;

        return instr switch
        {
            Binary b => IsDivMod(b.Op)
                        && !IsFloat(b.Src1) && !IsFloat(b.Src2) && !IsFloat(b.Dst)
                        && (Wide(b.Src1) || Wide(b.Src2) || Wide(b.Dst)),
            AugAssign aa => IsDivMod(aa.Op) && Wide(aa.Target),
            GcRoot or GcUnroot => true,
            InlineAsm ia => AsmClobbersTempPair(ia),
            _ => false,
        };
    }

    // Hand-written asm goes into the listing as text the allocator cannot model. With operands
    // the codegen stages %0..%3 through R16..R19, so the pair is written whatever the template
    // says. Without them, the template writes the pair when it names R16 or R17, and any call
    // it makes can write it too. A temp homed in the pair across either came back holding the
    // asm's value: `(s + 300) + f()`, with `asm("ldi r16, 0x55")` inlined from f, printed
    // 26198 instead of 301, and `for i in range(s + 3)` with the same asm in the body ran once.
    private static readonly System.Text.RegularExpressions.Regex AsmTouchesPair = new(
        @"\b(r1[67]|r?call|icall|eicall)\b",
        System.Text.RegularExpressions.RegexOptions.IgnoreCase);

    private static bool AsmClobbersTempPair(InlineAsm ia)
        => ia.Operands is { Count: > 0 } || AsmTouchesPair.IsMatch(ia.Code);

    public static Dictionary<string, string> Allocate(Function func)
    {
        var intervals = new Dictionary<string, LiveInterval>();
        var callIndices = new HashSet<int>();

        void VisitVal(Val val, int i)
        {
            if (val is not Temporary t) return;
            if (intervals.TryGetValue(t.Name, out var iv))
            {
                iv.LastUse = i;
                // A temp that shows up at different widths (a union payload read at
                // member width under tag dispatch) must be homed for the widest use;
                // first-write-wins could grant a byte slot to a 16-bit access.
                if (t.Type.SizeOf() > iv.Type.SizeOf()) iv.Type = t.Type;
            }
            else
                intervals[t.Name] = new LiveInterval { Name = t.Name, Type = t.Type, Def = i, LastUse = i };
        }

        for (int i = 0; i < func.Body.Count; ++i)
        {
            var instr = func.Body[i];
            // IndirectCall and GcAlloc transfer control to a callee/allocator and clobber the
            // caller-saved scratch the same way a direct Call does, so a temp whose live range
            // spans them must be spilled rather than kept in R16/R17.
            if (instr is Call or IndirectCall or GcAlloc || ClobbersTempPair(instr)) callIndices.Add(i);

            switch (instr)
            {
                case Copy c:
                    VisitVal(c.Src, i);
                    VisitVal(c.Dst, i);
                    break;
                case Bitcast bc2:
                    VisitVal(bc2.Src, i);
                    VisitVal(bc2.Dst, i);
                    break;
                case Binary b:
                    VisitVal(b.Src1, i);
                    VisitVal(b.Src2, i);
                    VisitVal(b.Dst, i);
                    break;
                case Unary u:
                    VisitVal(u.Src, i);
                    VisitVal(u.Dst, i);
                    break;
                case Return r:
                    VisitVal(r.Value, i);
                    if (r.Tag != null) VisitVal(r.Tag, i);
                    break;
                case JumpIfZero jz: VisitVal(jz.Condition, i); break;
                case JumpIfNotZero jnz: VisitVal(jnz.Condition, i); break;
                case JumpIfEqual je:
                    VisitVal(je.Src1, i);
                    VisitVal(je.Src2, i);
                    break;
                case JumpIfNotEqual jne:
                    VisitVal(jne.Src1, i);
                    VisitVal(jne.Src2, i);
                    break;
                case JumpIfLessThan jlt:
                    VisitVal(jlt.Src1, i);
                    VisitVal(jlt.Src2, i);
                    break;
                case JumpIfLessOrEqual jle:
                    VisitVal(jle.Src1, i);
                    VisitVal(jle.Src2, i);
                    break;
                case JumpIfGreaterThan jgt:
                    VisitVal(jgt.Src1, i);
                    VisitVal(jgt.Src2, i);
                    break;
                case JumpIfGreaterOrEqual jge:
                    VisitVal(jge.Src1, i);
                    VisitVal(jge.Src2, i);
                    break;
                case BitCheck bc:
                    VisitVal(bc.Source, i);
                    VisitVal(bc.Dst, i);
                    break;
                case BitWrite bw:
                    VisitVal(bw.Target, i);
                    VisitVal(bw.Src, i);
                    break;
                case BitSet bs: VisitVal(bs.Target, i); break;
                case BitClear bcl: VisitVal(bcl.Target, i); break;
                case AugAssign aa:
                    VisitVal(aa.Target, i);
                    VisitVal(aa.Operand, i);
                    break;
                case JumpIfBitSet jbs: VisitVal(jbs.Source, i); break;
                case JumpIfBitClear jbc: VisitVal(jbc.Source, i); break;
                case Call cl:
                    VisitVal(cl.Dst, i);
                    if (cl.TagDst != null) VisitVal(cl.TagDst, i);
                    foreach (var a in cl.Args) VisitVal(a, i);
                    break;
                case FlashLoadPtr flp:
                    VisitVal(flp.Ptr, i);
                    VisitVal(flp.Index, i);
                    VisitVal(flp.Dst, i);
                    break;
                case LoadIndirect li:
                    VisitVal(li.SrcPtr, i);
                    VisitVal(li.Dst, i);
                    break;
                case StoreIndirect si:
                    VisitVal(si.DstPtr, i);
                    VisitVal(si.Src, i);
                    break;
                // Array/bytearray ops were absent here, so a temp DEFINED by an ArrayLoad (or used
                // as an index/source) had no interval at that point -- its live range was seen as
                // starting only at a later consumer. Two temps that truly overlap (an earlier load
                // result still live while a second load's index is computed) then shared R16 and
                // clobbered each other (`arr[idx] + arr[s - 5]` returned just the second element).
                case ArrayLoad al2:
                    VisitVal(al2.Index, i);
                    VisitVal(al2.Dst, i);
                    break;
                case ArrayLoadFlash alf2:
                    VisitVal(alf2.Index, i);
                    VisitVal(alf2.Dst, i);
                    break;
                case ArrayStore ast2:
                    VisitVal(ast2.Index, i);
                    VisitVal(ast2.Src, i);
                    break;
                case BytearrayLoad bld2:
                    VisitVal(bld2.Index, i);
                    VisitVal(bld2.Dst, i);
                    break;
                case BytearrayStore bst2:
                    VisitVal(bst2.Index, i);
                    VisitVal(bst2.Src, i);
                    break;
                // Same omission as the array ops: an indirect call's result and a GC allocation's
                // pointer are temps that must be tracked, or they share a slot with an overlapping
                // temp and get clobbered (`fps[0](s) + fps[1](s)` lost the first call's result).
                case IndirectCall ic:
                    VisitVal(ic.FuncAddr, i);
                    foreach (var a in ic.Args) VisitVal(a, i);
                    VisitVal(ic.Dst, i);
                    break;
                case GcAlloc ga:
                    VisitVal(ga.Size, i);
                    VisitVal(ga.Dst, i);
                    break;
                case SignalError se:
                    VisitVal(se.Code, i);
                    break;
            }
        }

        var labelIndex = new Dictionary<string, int>();
        for (int i = 0; i < func.Body.Count; ++i)
            if (func.Body[i] is Label lb)
                labelIndex[lb.Name] = i;

        var backEdges = new List<(int Target, int Jump)>();
        for (int i = 0; i < func.Body.Count; ++i)
        {
            string? tgt = func.Body[i] switch
            {
                Jump j => j.Target,
                JumpIfZero j => j.Target,
                JumpIfNotZero j => j.Target,
                JumpIfEqual j => j.Target,
                JumpIfNotEqual j => j.Target,
                JumpIfLessThan j => j.Target,
                JumpIfLessOrEqual j => j.Target,
                JumpIfGreaterThan j => j.Target,
                JumpIfGreaterOrEqual j => j.Target,
                JumpIfBitSet j => j.Target,
                JumpIfBitClear j => j.Target,
                _ => null,
            };
            if (tgt != null && labelIndex.TryGetValue(tgt, out int li) && li < i)
                backEdges.Add((li, i));
        }

        bool changed = true;
        while (changed)
        {
            changed = false;
            foreach (var iv in intervals.Values)
            {
                foreach (var (target, jump) in backEdges)
                {
                    if (iv.Def < target && iv.LastUse >= target && iv.LastUse < jump)
                    {
                        iv.LastUse = jump;
                        changed = true;
                    }
                }
            }
        }

        // Mark intervals that strictly span a Call
        foreach (var iv in intervals.Values)
        {
            foreach (int ci in callIndices)
            {
                if (iv.Def < ci && ci < iv.LastUse)
                {
                    iv.SpansCall = true;
                    break;
                }
            }
        }

        // Collect eligible (1- or 2-byte scalar temps that do not span a call), by def.
        // 16-bit temps were previously always spilled to stack slots; allowing them to
        // occupy the R16:R17 pair removes the store/reload traffic the codegen otherwise
        // emits around every uint16 temporary (StoreRegInto/LoadIntoReg already drive the
        // high byte via GetHighReg, so a pair-homed temp needs no codegen change).
        var eligible = intervals.Values
            .Where(iv => !iv.SpansCall && (iv.Type.SizeOf() == 1 || iv.Type.SizeOf() == 2))
            .OrderBy(iv => iv.Def)
            .ToList();

        // Two byte-slots: slot[0] = R16, slot[1] = R17. An 8-bit temp takes one slot; a
        // 16-bit temp takes the pair (R16:R17, low in R16). Greedy with last-use expiry.
        var result = new Dictionary<string, string>();
        var slot = new LiveInterval?[2];

        foreach (var iv in eligible)
        {
            for (int k = 0; k < 2; ++k)
                if (slot[k] != null && slot[k]!.LastUse < iv.Def)
                    slot[k] = null;

            if (iv.Type.SizeOf() == 2)
            {
                // Needs the whole pair free.
                if (slot[0] == null && slot[1] == null)
                {
                    result[iv.Name] = "R16";
                    slot[0] = iv;
                    slot[1] = iv;
                }
            }
            else
            {
                for (int k = 0; k < 2; ++k)
                    if (slot[k] == null)
                    {
                        result[iv.Name] = k == 0 ? "R16" : "R17";
                        slot[k] = iv;
                        break;
                    }
            }
        }

        return result;
    }
}