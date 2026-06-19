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

    public static Dictionary<string, string> Allocate(Function func)
    {
        var intervals = new Dictionary<string, LiveInterval>();
        var callIndices = new HashSet<int>();

        void VisitVal(Val val, int i)
        {
            if (val is not Temporary t) return;
            if (intervals.TryGetValue(t.Name, out var iv))
                iv.LastUse = i;
            else
                intervals[t.Name] = new LiveInterval { Name = t.Name, Type = t.Type, Def = i, LastUse = i };
        }

        for (int i = 0; i < func.Body.Count; ++i)
        {
            var instr = func.Body[i];
            // IndirectCall and GcAlloc transfer control to a callee/allocator and clobber the
            // caller-saved scratch the same way a direct Call does, so a temp whose live range
            // spans them must be spilled rather than kept in R16/R17.
            if (instr is Call or IndirectCall or GcAlloc) callIndices.Add(i);

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
                case Return r: VisitVal(r.Value, i); break;
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