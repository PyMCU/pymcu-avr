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

namespace PyMCU.Backend.Targets.AVR;

public class AvrAsmLine
{
    public enum LineType
    {
        Instruction,
        Label,
        Comment,
        Raw,
        Empty,
        /// <summary>Carries source file + line for linemap generation. Not emitted to .asm output.</summary>
        DebugMarker
    }

    public LineType Type;
    public string LabelText = "";
    public string Mnemonic = "";
    public string Op1 = "";
    public string Op2 = "";
    public string Content = "";
    public string DebugFile = "";
    public int    DebugLine;

    public static AvrAsmLine MakeInstruction(string m, string o1 = "", string o2 = "")
        => new() { Type = LineType.Instruction, Mnemonic = m, Op1 = o1, Op2 = o2 };

    public static AvrAsmLine MakeLabel(string l)
        => new() { Type = LineType.Label, LabelText = l };

    public static AvrAsmLine MakeComment(string c)
        => new() { Type = LineType.Comment, Content = c };

    public static AvrAsmLine MakeRaw(string r)
        => new() { Type = LineType.Raw, Content = r };

    public static AvrAsmLine MakeEmpty()
        => new() { Type = LineType.Empty };

    public static AvrAsmLine MakeDebugMarker(string file, int line)
        => new() { Type = LineType.DebugMarker, DebugFile = file, DebugLine = line };

    public override string ToString()
    {
        switch (Type)
        {
            case LineType.Instruction:
                if (string.IsNullOrEmpty(Op1)) return $"\t{Mnemonic}";
                if (string.IsNullOrEmpty(Op2)) return $"\t{Mnemonic}\t{Op1}";
                return $"\t{Mnemonic}\t{Op1}, {Op2}";
            case LineType.Label:
                return $"{LabelText}:";
            case LineType.Comment:
                return $"; {Content}";
            case LineType.Raw:
                return Content;
            default:
                return "";
        }
    }
}

public static class AvrPeephole
{
    // Mnemonics that write exactly their first operand register and nothing else
    // relevant to slot tracking. (STD/LDD are handled separately; LD/ST and the
    // pointer/multi-register writers are deliberately excluded so they hit the
    // conservative clear-all path.)
    private static readonly HashSet<string> SingleDstWriters = new()
    {
        "MOV", "LDI", "LDS", "IN", "POP", "CLR", "SER", "COM", "NEG", "INC", "DEC",
        "LSR", "LSL", "ASR", "ROR", "ROL", "SWAP", "AND", "ANDI", "OR", "ORI", "EOR",
        "ADD", "ADC", "SUB", "SUBI", "SBC", "SBCI", "BLD", "SBR", "CBR",
    };

    // Mnemonics that touch no general-purpose register (status flags, I/O bits,
    // memory stores, control). These leave slot tracking intact.
    private static readonly HashSet<string> NonRegWriters = new()
    {
        "CP", "CPC", "CPI", "TST", "OUT", "PUSH", "SBI", "CBI", "SBIS", "SBIC",
        "SBRS", "SBRC", "BST", "NOP", "SEC", "CLC", "SEI", "CLI", "SET", "CLT",
        "SEZ", "CLZ", "WDR", "SLEEP",
    };

    /// <summary>
    /// Basic-block-local store-to-load forwarding. <c>slotReg[off]</c> records the
    /// general-purpose register that currently holds the same value as stack slot
    /// <c>Y+off</c>. A reload that re-reads a slot the register already mirrors,
    /// and a store of a value already present in the slot, are removed. Tracking
    /// is invalidated whenever the mirrored register is overwritten and cleared
    /// entirely at any block boundary or unmodeled instruction.
    /// </summary>
    private static void ForwardStores(List<AvrAsmLine> lines, ref bool changed)
    {
        var slotReg = new Dictionary<int, int>();

        void KillReg(int r)
        {
            if (r < 0) return;
            List<int>? dead = null;
            foreach (var kv in slotReg)
                if (kv.Value == r) (dead ??= new()).Add(kv.Key);
            if (dead != null)
                foreach (var k in dead) slotReg.Remove(k);
        }

        for (int i = 0; i < lines.Count; i++)
        {
            var ln = lines[i];
            if (ln.Type is AvrAsmLine.LineType.Comment
                        or AvrAsmLine.LineType.Empty
                        or AvrAsmLine.LineType.DebugMarker)
                continue;
            if (ln.Type != AvrAsmLine.LineType.Instruction)
            {
                slotReg.Clear(); // label / raw / anything non-instruction
                continue;
            }

            switch (ln.Mnemonic)
            {
                case "STD":
                {
                    int off = ParseYOffset(ln.Op1);
                    int rx = ParseReg(ln.Op2);
                    if (off < 0 || rx < 0) { slotReg.Clear(); break; }
                    if (slotReg.TryGetValue(off, out int cur) && cur == rx)
                    {
                        // Slot already holds this register's (unchanged) value.
                        lines[i] = AvrAsmLine.MakeEmpty();
                        changed = true;
                        break;
                    }
                    slotReg[off] = rx;
                    break;
                }
                case "LDD":
                {
                    int off = ParseYOffset(ln.Op2);
                    int ry = ParseReg(ln.Op1);
                    if (off < 0 || ry < 0) { slotReg.Clear(); break; }
                    if (slotReg.TryGetValue(off, out int rx) && rx == ry)
                    {
                        // Register already mirrors this slot — redundant reload.
                        lines[i] = AvrAsmLine.MakeEmpty();
                        changed = true;
                        break;
                    }
                    KillReg(ry);          // ry is overwritten by the load
                    slotReg[off] = ry;    // ry now mirrors Y+off
                    break;
                }
                default:
                {
                    if (SingleDstWriters.Contains(ln.Mnemonic))
                        KillReg(ParseReg(ln.Op1));
                    else if (!NonRegWriters.Contains(ln.Mnemonic))
                        slotReg.Clear(); // unknown / multi-write / flow: forget everything
                    break;
                }
            }
        }
    }

    private static bool IsFlowTerminator(AvrAsmLine line)
    {
        return line.Type == AvrAsmLine.LineType.Instruction &&
               (line.Mnemonic == "RJMP" || line.Mnemonic == "JMP" ||
                line.Mnemonic == "RETI" || line.Mnemonic == "RET" ||
                line.Mnemonic.StartsWith("BR"));
    }

    private static int ParseYOffset(string s)
    {
        if (s.Length < 3 || s[0] != 'Y' || s[1] != '+') return -1;
        return int.TryParse(s.AsSpan(2), out int v) ? v : -1;
    }

    private static int ParseReg(string s)
    {
        if (s.Length < 2 || s[0] != 'R') return -1;
        return int.TryParse(s.AsSpan(1), out int v) ? v : -1;
    }

    public static List<AvrAsmLine> Optimize(List<AvrAsmLine> lines)
    {
        var result = new List<AvrAsmLine>(lines);
        var changed = true;

        while (changed)
        {
            changed = false;
            var next = new List<AvrAsmLine>();

            // --- Dead Label Elimination ---
            var usedLabels = new HashSet<string>();
            foreach (var line in result.Where(line => line.Type == AvrAsmLine.LineType.Instruction))
            {
                if (line.Mnemonic is "RJMP" or "RCALL" or "JMP" or "CALL" or
                    "BREQ" or "BRNE" or "BRLO" or "BRSH" or "BRMI" or "BRPL" or
                    "BRLT" or "BRGE" or "BRCS" or "BRCC" or
                    "BRTS" or "BRTC")
                {
                    usedLabels.Add(line.Op1);
                }
            }

            usedLabels.Add("main");
            usedLabels.Add("__vector_default");

            int modCtr = 0;
            var aliases = new string[32];
            for (int k = 0; k < 32; ++k)
                aliases[k] = $"init_{modCtr++}";

            for (int i = 0; i < result.Count; ++i)
            {
                var current = result[i];

                if (current.Type == AvrAsmLine.LineType.Label)
                {
                    if (!usedLabels.Contains(current.LabelText) &&
                        (current.LabelText.StartsWith("L.") || current.LabelText.StartsWith("L_")))
                    {
                        changed = true;
                        continue;
                    }

                    for (int k = 0; k < 32; ++k)
                        aliases[k] = $"lab_{modCtr++}";
                    
                    next.Add(current);
                    continue;
                }

                if (current.Type != AvrAsmLine.LineType.Instruction)
                {
                    // Raw inline assembly may modify any register — invalidate
                    // all alias tracking so that subsequent MOV/LDI instructions
                    // are not incorrectly eliminated as redundant.
                    if (current.Type == AvrAsmLine.LineType.Raw)
                    {
                        for (int k = 0; k < 32; ++k)
                            aliases[k] = $"raw_{modCtr++}";
                    }
                    next.Add(current);
                    continue;
                }

                switch (current.Mnemonic)
                {
                    case "LDI":
                    {
                        var regIdx = ParseReg(current.Op1);
                        if (regIdx is >= 0 and < 32)
                        {
                            var valId = "ldi_" + current.Op2;
                            if (aliases[regIdx] == valId)
                            {
                                changed = true;
                                continue; // Redundant LDI
                            }
                            aliases[regIdx] = valId;
                        }
                        break;
                    }
                    case "MOV":
                    {
                        var dstIdx = ParseReg(current.Op1);
                        var srcIdx = ParseReg(current.Op2);
                        switch (dstIdx)
                        {
                            case >= 0 and < 32 when srcIdx is >= 0 and < 32:
                            {
                                if (aliases[dstIdx] == aliases[srcIdx])
                                {
                                    changed = true;
                                    continue; // Redundant MOV
                                }
                                aliases[dstIdx] = aliases[srcIdx];
                                break;
                            }
                            case >= 0 and < 32:
                                aliases[dstIdx] = $"mov_{modCtr++}";
                                break;
                        }
                        break;
                    }
                    case "STS" or "OUT" or "CP" or "TST" or "STD" or "SBI" or "CBI" or "SBIS" or "SBIC":
                        break;
                    case "LDS" or "IN" or "LDD":
                    {
                        var regIdx = ParseReg(current.Op1);
                        if (regIdx is >= 0 and < 32)
                            aliases[regIdx] = $"load_{modCtr++}";
                        break;
                    }
                    case "CLR":
                    {
                        var regIdx = ParseReg(current.Op1);
                        if (regIdx is >= 0 and < 32)
                            aliases[regIdx] = "ldi_0";
                        break;
                    }
                    case "MUL" or "MULS" or "MULSU" or "FMUL" or "FMULS" or "FMULSU":
                    {
                        // MUL family writes to R1:R0, NOT to its operands.
                        aliases[0] = $"mod_{modCtr++}";
                        aliases[1] = $"mod_{modCtr++}";
                        break;
                    }
                    case "ADD" or "SUB" or "INC" or "DEC" or "NEG" or "COM" or
                         "ORI" or "ANDI" or "EOR" or "AND" or "OR" or "ADC" or "SBC" or "LSR" or
                         "ASR" or "ROR" or "LSL" or "ROL" or "CPC" or "CPI":
                    {
                        int regIdx = ParseReg(current.Op1);
                        if (regIdx is >= 0 and < 32)
                            aliases[regIdx] = $"mod_{modCtr++}";
                        break;
                    }
                    default:
                    {
                        if (IsFlowTerminator(current))
                        {
                            if (current.Mnemonic == "RJMP")
                            {
                                var redundant = false;
                                for (var j = i + 1; j < result.Count; ++j)
                                {
                                    if (result[j].Type == AvrAsmLine.LineType.Label)
                                    {
                                        if (result[j].LabelText == current.Op1) redundant = true;
                                        break;
                                    }
                                    if (result[j].Type is AvrAsmLine.LineType.Comment or AvrAsmLine.LineType.Empty)
                                        continue;
                                    break;
                                }

                                if (redundant)
                                {
                                    changed = true;
                                    continue;
                                }
                            }

                            next.Add(current);
                            for (int k = 0; k < 32; ++k)
                                aliases[k] = $"flow_{modCtr++}";
                            continue;
                        }
                        else
                        {
                            for (int k = 0; k < 32; ++k)
                                aliases[k] = $"unk_{modCtr++}";
                        }
                        break;
                    }
                }

                next.Add(current);
            }
            result = next;
        }

        bool fwdChanged = true;
        while (fwdChanged)
        {
            fwdChanged = false;
            for (int i = 0; i + 1 < result.Count; ++i)
            {
                if (result[i].Type != AvrAsmLine.LineType.Instruction) continue;

                int j = i + 1;
                while (j < result.Count && result[j].Type is AvrAsmLine.LineType.Comment or AvrAsmLine.LineType.Empty) j++;
                if (j >= result.Count || result[j].Type != AvrAsmLine.LineType.Instruction) continue;

                var a = result[i];
                var b = result[j];

                // Pattern A: STD Y+N, Rx ; LDD Ry, Y+N
                if (a.Mnemonic == "STD" && b.Mnemonic == "LDD")
                {
                    int aOff = ParseYOffset(a.Op1);
                    int bOff = ParseYOffset(b.Op2);
                    if (aOff >= 0 && aOff == bOff)
                    {
                        int rx = ParseReg(a.Op2);
                        int ry = ParseReg(b.Op1);
                        if (rx >= 0 && ry >= 0)
                        {
                            if (rx == ry)
                            {
                                result[j] = AvrAsmLine.MakeEmpty();
                            }
                            else
                            {
                                result[j] = AvrAsmLine.MakeInstruction("MOV", $"R{ry}", $"R{rx}");
                            }
                            fwdChanged = true;
                        }
                    }
                }

                // Pattern B: LDD Rx, Y+N ; STD Y+N, Rx
                if (a.Mnemonic == "LDD" && b.Mnemonic == "STD")
                {
                    var aOff = ParseYOffset(a.Op2);
                    var bOff = ParseYOffset(b.Op1);
                    if (aOff >= 0 && aOff == bOff && a.Op1 == b.Op2)
                    {
                        result[j] = AvrAsmLine.MakeEmpty();
                        fwdChanged = true;
                    }
                }
            }
        }
        // MOV Ra, Rb; OP Ra; MOV Rb, Ra -> OP Rb; MOV Ra, Rb
        var win3 = true;
        while (win3)
        {
            win3 = false;
            for (var i = 0; i < result.Count; ++i)
            {
                if (result[i].Type != AvrAsmLine.LineType.Instruction) continue;
                
                var j2 = i + 1;
                while (j2 < result.Count && result[j2].Type is AvrAsmLine.LineType.Comment or AvrAsmLine.LineType.Empty) ++j2;
                if (j2 >= result.Count || result[j2].Type != AvrAsmLine.LineType.Instruction) continue;
                
                var k2 = j2 + 1;
                while (k2 < result.Count && result[k2].Type is AvrAsmLine.LineType.Comment or AvrAsmLine.LineType.Empty) ++k2;
                if (k2 >= result.Count || result[k2].Type != AvrAsmLine.LineType.Instruction) continue;

                var a2 = result[i];
                var b2 = result[j2];
                var c2 = result[k2];

                if (a2.Mnemonic != "MOV" || c2.Mnemonic != "MOV" ||
                    b2.Mnemonic is not ("INC" or "DEC" or "COM" or "NEG") ||
                    b2.Op1 != a2.Op1 || c2.Op2 != a2.Op1 || c2.Op1 != a2.Op2) continue;

                var ra = a2.Op1; // Ej. R24
                var rb = a2.Op2; // Ej. R4

                result[i] = AvrAsmLine.MakeEmpty(); 
                result[j2] = AvrAsmLine.MakeInstruction(b2.Mnemonic, rb); // INC R4
                result[k2] = AvrAsmLine.MakeInstruction("MOV", ra, rb);   // MOV R24, R4

                win3 = true;
            }

            result.RemoveAll(l => l.Type == AvrAsmLine.LineType.Empty);
        }

        // --- Basic-block store-to-load forwarding & redundant load/store removal ---
        // The adjacent STD/LDD patterns above only fire when the store and the
        // reload sit next to each other. 16-bit values interleave their lo/hi
        // halves (STD lo; STD hi; LDD lo; LDD hi), and call results get parked in
        // a slot then immediately reloaded for a compare, so the redundant reload
        // is never adjacent to its store. ForwardStores tracks, per basic block,
        // which register still mirrors each Y+offset slot and drops reloads that
        // re-read a value the register already holds (and re-stores of an
        // unchanged value). It is conservative by construction: anything it does
        // not explicitly model as a pure reader or single-register writer clears
        // all tracking, so it never forwards a stale value across an unknown
        // effect, a call, a branch, or a label.
        var sfChanged = true;
        while (sfChanged)
        {
            sfChanged = false;
            ForwardStores(result, ref sfChanged);
            if (sfChanged)
                result.RemoveAll(l => l.Type == AvrAsmLine.LineType.Empty);
        }

        // --- Dead temporary-register move elimination (R16/R17) ---
        EliminateDeadTempMoves(result);

        // --- Conditional branch shortening ---
        ShortenBranches(result);

        result.RemoveAll(l => l.Type == AvrAsmLine.LineType.Empty);
        return result;
    }

    // Inverse pairs for the AVR status-flag conditional branches. EmitBranch lowers a
    // "branch to target if cond" as `inv(cond) skip; RJMP target; skip:` so the RJMP can
    // reach any distance. When target is within a conditional branch's own ±64-word reach,
    // the whole triple collapses back to a single `cond target`.
    private static readonly Dictionary<string, string> BranchInverse = new()
    {
        { "BREQ", "BRNE" }, { "BRNE", "BREQ" }, { "BRCS", "BRCC" }, { "BRCC", "BRCS" },
        { "BRSH", "BRLO" }, { "BRLO", "BRSH" }, { "BRLT", "BRGE" }, { "BRGE", "BRLT" },
        { "BRMI", "BRPL" }, { "BRPL", "BRMI" }, { "BRVS", "BRVC" }, { "BRVC", "BRVS" },
        { "BRHS", "BRHC" }, { "BRHC", "BRHS" }, { "BRTS", "BRTC" }, { "BRTC", "BRTS" },
        { "BRIE", "BRID" }, { "BRID", "BRIE" },
    };

    // Word size of a line for branch-distance accounting. Returns false for anything whose
    // size (or effect on the address counter) we cannot account for — Raw inline-asm and
    // assembler directives — so a candidate whose span contains one is left untouched.
    private static bool TryWordSize(AvrAsmLine ln, out int words)
    {
        words = 0;
        switch (ln.Type)
        {
            case AvrAsmLine.LineType.Instruction:
                words = ln.Mnemonic is "CALL" or "JMP" or "LDS" or "STS" ? 2 : 1;
                return true;
            case AvrAsmLine.LineType.Label:
            case AvrAsmLine.LineType.Comment:
            case AvrAsmLine.LineType.Empty:
            case AvrAsmLine.LineType.DebugMarker:
                return true;   // occupy no flash
            default:
                return false;  // Raw / unknown -> bail this candidate
        }
    }

    private static int NextSignificant(List<AvrAsmLine> lines, int idx)
    {
        for (int x = idx + 1; x < lines.Count; ++x)
            if (lines[x].Type is not (AvrAsmLine.LineType.Comment
                                       or AvrAsmLine.LineType.Empty
                                       or AvrAsmLine.LineType.DebugMarker))
                return x;
        return -1;
    }

    private static int CountLabelRefs(List<AvrAsmLine> lines, string label)
    {
        int c = 0;
        foreach (var ln in lines)
            if (ln.Type == AvrAsmLine.LineType.Instruction && ln.Op1 == label
                && (ln.Mnemonic.StartsWith("BR") || ln.Mnemonic is "RJMP" or "JMP" or "RCALL" or "CALL"))
                ++c;
        return c;
    }

    // Branch displacement k in words, where the target is reached as PC+1+k. Returns null
    // when the span between branch and target contains an unsizable line.
    private static int? BranchDisplacement(List<AvrAsmLine> lines, int branchIdx, int targetIdx)
    {
        int sum = 0;
        if (targetIdx > branchIdx)
        {
            for (int x = branchIdx + 1; x < targetIdx; ++x)
            {
                if (!TryWordSize(lines[x], out int w)) return null;
                sum += w;
            }
            return sum;                 // forward
        }
        for (int x = targetIdx; x < branchIdx; ++x)
        {
            if (!TryWordSize(lines[x], out int w)) return null;
            sum += w;
        }
        return -sum - 1;                // backward
    }

    // Collapse `inv(cond) skip; RJMP target; skip:` into `cond target` whenever target is
    // within the conditional branch's ±64-word reach. Shortening only ever reduces the
    // distance seen by other branches, so the fixed-point loop is monotone and a candidate
    // validated in the current (longer) layout stays in range after every later removal.
    // If a displacement is mis-estimated as in-range, the assembler rejects the build — a
    // loud, test-caught failure, never silently wrong code.
    private static void ShortenBranches(List<AvrAsmLine> lines)
    {
        bool changed = true;
        while (changed)
        {
            changed = false;
            for (int i = 0; i < lines.Count; ++i)
            {
                var br = lines[i];
                if (br.Type != AvrAsmLine.LineType.Instruction) continue;
                if (!BranchInverse.ContainsKey(br.Mnemonic)) continue;

                int j = NextSignificant(lines, i);
                if (j < 0 || lines[j].Mnemonic != "RJMP") continue;
                int k = NextSignificant(lines, j);
                if (k < 0 || lines[k].Type != AvrAsmLine.LineType.Label) continue;
                if (lines[k].LabelText != br.Op1) continue;       // the skip label
                if (CountLabelRefs(lines, br.Op1) != 1) continue;  // referenced only here

                string target = lines[j].Op1;
                int targetIdx = lines.FindIndex(l => l.Type == AvrAsmLine.LineType.Label
                                                     && l.LabelText == target);
                if (targetIdx < 0) continue;

                int? disp = BranchDisplacement(lines, i, targetIdx);
                if (disp is null or < -64 or > 63) continue;

                lines[i] = AvrAsmLine.MakeInstruction(BranchInverse[br.Mnemonic], target);
                lines.RemoveAt(k);   // remove higher index first
                lines.RemoveAt(j);
                changed = true;
                break;               // restart: indices and distances have shifted
            }
        }
    }

    // Registers the linear-scan allocator uses as short-lived temporaries (never
    // call-saved/restored, never a return register). A MOV that writes one of these
    // and is never read afterwards is the home-store of a call result the comparison
    // already consumed from R24 -- pure dead weight.
    private static readonly int[] TempRegs = { 16, 17 };

    // Mnemonics whose first operand is a pure destination (written, not read).
    private static readonly HashSet<string> PureWriteOp1 = new()
    {
        "MOV", "LDI", "LDD", "LDS", "IN", "POP", "CLR", "SER", "LPM", "ELPM",
    };

    /// <summary>
    /// Removes <c>MOV R16/R17, Rs</c> instructions whose destination is dead — never
    /// read on any path before being overwritten or before the function returns.
    ///
    /// Uses a backward liveness restricted to the two temporary registers, which makes
    /// the exit condition exact (R16/R17 are scratch: not return values and, if ever
    /// pushed, restored by a POP that kills the value first, so they are dead at every
    /// RET). Safety is asymmetric by design: reads are over-approximated (unknown
    /// mnemonics, raw inline asm and calls are treated as reading the temps, keeping
    /// them live) while writes are under-approximated (only an unambiguous pure write
    /// kills liveness). The pass bails entirely on computed jumps it cannot resolve.
    /// </summary>
    private static void EliminateDeadTempMoves(List<AvrAsmLine> lines)
    {
        int n = lines.Count;
        if (n == 0) return;

        var labelIndex = new Dictionary<string, int>();
        for (int i = 0; i < n; i++)
            if (lines[i].Type == AvrAsmLine.LineType.Label)
                labelIndex[lines[i].LabelText] = i;

        // Successors per line. null entry => bail (unresolved control flow).
        var succ = new List<int>[n];
        for (int i = 0; i < n; i++)
        {
            var line = lines[i];
            if (line.Type != AvrAsmLine.LineType.Instruction)
            {
                succ[i] = i + 1 < n ? new List<int> { i + 1 } : new List<int>();
                continue;
            }

            string m = line.Mnemonic;
            if (m is "RET" or "RETI")
            {
                succ[i] = new List<int>(); // exit: temps are dead here
            }
            else if (m is "RJMP" or "JMP")
            {
                if (!labelIndex.TryGetValue(line.Op1, out int t)) return; // unknown target -> bail
                succ[i] = new List<int> { t };
            }
            else if (m is "IJMP" or "EIJMP")
            {
                return; // computed jump -> bail
            }
            else if (m.StartsWith("BR"))
            {
                var s = new List<int>();
                if (labelIndex.TryGetValue(line.Op1, out int t)) s.Add(t);
                else return; // unknown branch target -> bail
                if (i + 1 < n) s.Add(i + 1);
                succ[i] = s;
            }
            else
            {
                // Everything else (incl. CALL/RCALL/ICALL) falls through.
                succ[i] = i + 1 < n ? new List<int> { i + 1 } : new List<int>();
            }
        }

        // Raw lines may be hand-written asm with arbitrary register effects; treat them
        // as reading the temps so nothing around them is touched.
        // Assembler directives (.equ/.org/.byte/.global/...) emit no code and touch no
        // register; only hand-written inline-asm Raw lines have unknown register effects.
        static bool IsDirective(AvrAsmLine l)
            => l.Type == AvrAsmLine.LineType.Raw && l.Content.TrimStart().StartsWith(".");

        bool Reads(AvrAsmLine line, int r)
        {
            if (line.Type == AvrAsmLine.LineType.Raw)
                // Hand-written inline asm: assume it reads the temp only if the register
                // name appears in its text (so register-free Raw like NOP delays stay
                // transparent). Over-approximate — any textual mention counts as a read.
                return !IsDirective(line)
                       && line.Content.Contains("R" + r, StringComparison.OrdinalIgnoreCase);
            if (line.Type != AvrAsmLine.LineType.Instruction) return false;
            string m = line.Mnemonic;
            // CALL/RCALL/ICALL do NOT read the temp registers: PyMCU passes arguments in
            // R24/R22/R20/R18 (never R16/R17), and callees that use a temp internally
            // (e.g. __div8) push/pop it rather than taking it as input. The operand of a
            // call is a label, so it reads no register here either way.
            if (PureWriteOp1.Contains(m)) return ParseReg(line.Op2) == r; // Op1 is the destination
            return ParseReg(line.Op1) == r || ParseReg(line.Op2) == r;
        }

        bool PureWrites(AvrAsmLine line, int r)
        {
            if (line.Type != AvrAsmLine.LineType.Instruction) return false;
            return PureWriteOp1.Contains(line.Mnemonic)
                   && ParseReg(line.Op1) == r
                   && ParseReg(line.Op2) != r;
        }

        // Backward liveness fixpoint, one bit per temp register.
        int t0 = TempRegs.Length;
        var liveIn = new bool[n, t0];
        bool changed = true;
        while (changed)
        {
            changed = false;
            for (int i = n - 1; i >= 0; i--)
            {
                for (int ti = 0; ti < t0; ti++)
                {
                    int r = TempRegs[ti];
                    bool outLive = false;
                    foreach (int s in succ[i]) outLive |= liveIn[s, ti];
                    bool inLive = Reads(lines[i], r) || (!PureWrites(lines[i], r) && outLive);
                    if (inLive != liveIn[i, ti]) { liveIn[i, ti] = inLive; changed = true; }
                }
            }
        }

        // Remove MOV <temp>, Rs whose destination is dead on exit.
        for (int i = 0; i < n; i++)
        {
            var line = lines[i];
            if (line.Type != AvrAsmLine.LineType.Instruction || line.Mnemonic != "MOV") continue;
            int dst = ParseReg(line.Op1);
            int src = ParseReg(line.Op2);
            int ti = Array.IndexOf(TempRegs, dst);
            if (ti < 0 || src < 0 || src == dst) continue;

            bool outLive = false;
            foreach (int s in succ[i]) outLive |= liveIn[s, ti];
            if (!outLive) lines[i] = AvrAsmLine.MakeEmpty();
        }
    }
}