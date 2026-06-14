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

        // --- Redundant variable-index array-address (Z) materialization removal ---
        // arr[i] (variable i) lowers to a 6-instruction recipe building base+i*scale
        // into Z, then LD/ST through Z (which leaves Z intact). Two accesses to the same
        // array with the same index in one straight-line region rebuild the identical
        // recipe; with the index source and Z untouched between them, the rebuild is
        // dead. Runs before ForwardStores so the canonical recipe is still intact.
        var zsChanged = true;
        while (zsChanged)
        {
            zsChanged = false;
            EliminateRedundantZSetup(result, ref zsChanged);
            if (zsChanged)
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

        // --- Fuse adjacent immediate ORI/ANDI on the same register ---
        // The @inline driver composition emits split constant masks (e.g.
        // (nib & 0xF0) | _RS | _BL -> ORI Rd,1; ORI Rd,8). Runs after the dead
        // temp-move pass so the staging MOV the codegen parks between the two ops
        // is gone, leaving them consecutive. Folding them is a flat win:
        // Rd = (Rd op a) op b == Rd op (a op b) — the second op alone fixes Rd and
        // SREG, and nothing significant reads the intermediate.
        var fiChanged = true;
        while (fiChanged)
        {
            fiChanged = false;
            FuseImmediates(result, ref fiChanged);
            if (fiChanged)
                result.RemoveAll(l => l.Type == AvrAsmLine.LineType.Empty);
        }

        // --- Conditional branch shortening ---
        ShortenBranches(result);

        // --- Redundant register-reload elimination across branches ---
        // After `MOV d,s` both registers hold the same value, but the main alias
        // pass clears its facts at every conditional branch (a join-point
        // precaution), so a reload `MOV s,d` the codegen emits after a
        // compare+branch survives even though the register is unchanged on the
        // fall-through (the decimal-print's `MOV R9,R24; CPI; BRLO; MOV R24,R9`).
        // Runs AFTER ShortenBranches, which collapses the `BRcc skip; RJMP t; skip:`
        // lowering back to a single `BRcc t` and removes the skip label that would
        // otherwise (conservatively) stop the scan.
        var rrChanged = true;
        while (rrChanged)
        {
            rrChanged = false;
            EliminateRedundantReloads(result, ref rrChanged);
            if (rrChanged)
                result.RemoveAll(l => l.Type == AvrAsmLine.LineType.Empty);
        }

        // --- Park/unpark round-trip elimination (call-argument shuffle) ---
        // `MOV Rh,Rs; <set up other args>; MOV Rd,Rh` parks a value in a home and
        // reloads it because Rs (R24) is reused for another argument. When nothing
        // between touches Rh or Rd and Rh is dead afterward, write the value straight
        // into Rd instead and drop the reload.
        var prChanged = true;
        while (prChanged)
        {
            prChanged = false;
            EliminateParkRoundTrip(result, ref prChanged);
            if (prChanged)
                result.RemoveAll(l => l.Type == AvrAsmLine.LineType.Empty);
        }

        // --- Dead pure-write elimination (any register) ---
        // Runs last: earlier passes (park/reload removal, immediate fusion) expose writes
        // whose result is never read before being overwritten — e.g. the redundant high-byte
        // CLR the 16-bit operand widening parks before reusing the register.
        var dsChanged = true;
        while (dsChanged)
        {
            dsChanged = false;
            EliminateDeadStores(result, ref dsChanged);
            if (dsChanged)
                result.RemoveAll(l => l.Type == AvrAsmLine.LineType.Empty);
        }

        result.RemoveAll(l => l.Type == AvrAsmLine.LineType.Empty);
        return result;
    }

    // Fuses two consecutive same-register immediate ops (ORI Rd,a; ORI Rd,b ->
    // ORI Rd,a|b; likewise ANDI -> a&b). The two need only be consecutive among
    // *significant* lines — comments/empties/debug markers between them are fine —
    // but a label or raw asm between bails (control could enter, or Rd/flags be
    // touched, between them).
    private static void FuseImmediates(List<AvrAsmLine> lines, ref bool changed)
    {
        for (int i = 0; i < lines.Count; i++)
        {
            var a = lines[i];
            if (a.Type != AvrAsmLine.LineType.Instruction) continue;
            if (a.Mnemonic is not ("ORI" or "ANDI")) continue;

            int j = NextSignificant(lines, i);
            if (j < 0) continue;
            var b = lines[j];
            if (b.Type != AvrAsmLine.LineType.Instruction) continue;   // label / raw between
            if (b.Mnemonic != a.Mnemonic || b.Op1 != a.Op1) continue;  // same op, same Rd
            if (!TryParseImm8(a.Op2, out int va) || !TryParseImm8(b.Op2, out int vb)) continue;

            // Rd = (Rd op a) op b == Rd op (a op b); b alone fixes Rd and SREG and
            // nothing significant reads the intermediate, so flags are preserved.
            int fused = a.Mnemonic == "ORI" ? (va | vb) : (va & vb);
            lines[i] = AvrAsmLine.MakeEmpty();
            lines[j] = AvrAsmLine.MakeInstruction(a.Mnemonic, a.Op1, fused.ToString());
            changed = true;
        }
    }

    // True for an assembler directive Raw line (.equ/.org/.byte/...): emits no code,
    // touches no register. Hand-written inline asm Raw lines are NOT directives.
    private static bool IsDirectiveLine(AvrAsmLine l)
        => l.Type == AvrAsmLine.LineType.Raw && l.Content.TrimStart().StartsWith(".");

    // Instructions whose only register effect is writing operand 1.
    private static readonly HashSet<string> WritesOp1Only = new()
    {
        "MOV", "LDI", "LD", "LDD", "LDS", "IN", "POP", "LPM", "ELPM", "CLR", "SER", "COM",
        "NEG", "INC", "DEC", "LSL", "LSR", "ASR", "ROL", "ROR", "SWAP", "ADD", "ADC", "SUB",
        "SUBI", "SBC", "SBCI", "AND", "ANDI", "OR", "ORI", "EOR", "BLD",
    };

    // Instructions that touch flags / memory / PC / SP only — no general-purpose
    // register destination.
    private static readonly HashSet<string> WritesNoReg = new()
    {
        "CP", "CPC", "CPI", "CPSE", "TST", "ST", "STD", "STS", "OUT", "PUSH", "SBI", "CBI",
        "SBIS", "SBIC", "SBRC", "SBRS", "NOP", "RJMP", "JMP", "SEI", "CLI", "WDR", "SLEEP",
        "BREAK", "RET", "RETI", "SEC", "CLC", "SEZ", "CLZ",
    };

    // Conservatively over-approximates whether `line` may write register r. Used by the
    // redundant-reload pass to decide when a tracked value is still intact, so it MUST
    // err toward "yes" (anything unrecognised counts as a write, ending the scan).
    private static bool WritesReg(AvrAsmLine line, int r)
    {
        switch (line.Type)
        {
            case AvrAsmLine.LineType.Comment:
            case AvrAsmLine.LineType.Empty:
            case AvrAsmLine.LineType.DebugMarker:
            case AvrAsmLine.LineType.Label:
                return false;
            case AvrAsmLine.LineType.Raw:
                return !IsDirectiveLine(line);    // unknown hand-written asm -> assume it writes
        }

        string m = line.Mnemonic;
        if (m.StartsWith("BR")) return false;                       // conditional/relative branches
        if (WritesNoReg.Contains(m)) return false;
        // Calls clobber the scratch/arg/return registers; R4-R15, the Y pointer and the
        // zero register survive (PyMCU never uses R4-R15 as scratch).
        if (m is "CALL" or "RCALL" or "ICALL" or "EICALL")
            return r is not ((>= 4 and <= 15) or 28 or 29 or 1);
        if (m is "MUL" or "MULS" or "MULSU" or "FMUL" or "FMULS" or "FMULSU")
            return r is 0 or 1;
        if (m is "MOVW" or "ADIW" or "SBIW")
        {
            int d = ParseReg(line.Op1);
            return d == r || d + 1 == r;
        }
        if (WritesOp1Only.Contains(m))
            return ParseReg(line.Op1) == r;
        return true;                                                // unrecognised -> conservative
    }

    // Removes a `MOV x,y` whose {x,y} both already hold the same value because a prior
    // `MOV d,s` (with {x,y} == {d,s}) made them equal and nothing wrote either register
    // in between. Stops at any write of d or s and at any label (a possible alternate
    // entry where the equality may not hold).
    private static void EliminateRedundantReloads(List<AvrAsmLine> lines, ref bool changed)
    {
        for (int i = 0; i < lines.Count; i++)
        {
            if (lines[i].Type != AvrAsmLine.LineType.Instruction || lines[i].Mnemonic != "MOV")
                continue;
            int d = ParseReg(lines[i].Op1), s = ParseReg(lines[i].Op2);
            if (d < 0 || s < 0 || d == s) continue;

            for (int j = i + 1; j < lines.Count; j++)
            {
                var lj = lines[j];
                if (lj.Type is AvrAsmLine.LineType.Comment or AvrAsmLine.LineType.Empty
                            or AvrAsmLine.LineType.DebugMarker) continue;
                if (lj.Type == AvrAsmLine.LineType.Label) break;

                if (lj.Type == AvrAsmLine.LineType.Instruction && lj.Mnemonic == "MOV")
                {
                    int x = ParseReg(lj.Op1), y = ParseReg(lj.Op2);
                    if ((x == d && y == s) || (x == s && y == d))
                    {
                        lines[j] = AvrAsmLine.MakeEmpty();   // copies a value the dest already holds
                        changed = true;
                        continue;                            // {d,s} still equal; keep scanning
                    }
                }

                if (WritesReg(lj, d) || WritesReg(lj, s)) break;
            }
        }
    }

    // Dead pure-write elimination on any register. A pure-write (MOV/LDI/LDD/LDS/IN/POP/
    // CLR/SER/LPM into op1) whose destination is overwritten by another pure-write before any
    // read is dead. The forward scan stops — keeping the instruction — at the first read of the
    // register, any control-flow (branch/jump/call/ret/skip), label, or raw line, so flow can
    // never bypass the overwrite and a call/branch can never observe the value. CLR sets SREG,
    // so a dead CLR is removed only when the overwriting write is itself a CLR (re-establishing
    // identical flags); the other pure-writes leave SREG untouched, so dropping them is
    // flag-neutral. This catches the doubled `CLR Rh` the 16-bit operand widening emits and any
    // MOV/LDI/LDD reload of a value that is rewritten before use.
    private static void EliminateDeadStores(List<AvrAsmLine> lines, ref bool changed)
    {
        for (int i = 0; i < lines.Count; i++)
        {
            var li = lines[i];
            if (li.Type != AvrAsmLine.LineType.Instruction || !PureWriteOp1.Contains(li.Mnemonic))
                continue;
            int r = ParseReg(li.Op1);
            if (r < 0) continue;
            if (li.Mnemonic == "MOV" && ParseReg(li.Op2) == r) continue;
            bool iSetsFlags = li.Mnemonic == "CLR";   // only CLR (= EOR Rd,Rd) touches SREG here

            for (int j = i + 1; j < lines.Count; j++)
            {
                var lj = lines[j];
                if (lj.Type is AvrAsmLine.LineType.Comment or AvrAsmLine.LineType.Empty
                            or AvrAsmLine.LineType.DebugMarker) continue;
                if (lj.Type != AvrAsmLine.LineType.Instruction) break;   // label / raw -> stop
                string m = lj.Mnemonic;
                if (m.StartsWith("BR") || m is "RJMP" or "JMP" or "IJMP" or "EIJMP"
                    or "CALL" or "RCALL" or "ICALL" or "EICALL" or "RET" or "RETI"
                    or "SBRC" or "SBRS" or "SBIC" or "SBIS" or "CPSE") break;
                if (ReadsReg(lj, r)) break;
                if (WritesReg(lj, r))
                {
                    bool jPureWrite = PureWriteOp1.Contains(m) && ParseReg(lj.Op1) == r
                                      && !(m == "MOV" && ParseReg(lj.Op2) == r);
                    if (jPureWrite && (!iSetsFlags || m == "CLR"))
                    {
                        lines[i] = AvrAsmLine.MakeEmpty();
                        changed = true;
                    }
                    break;
                }
            }
        }
    }

    private static bool TryParseImm8(string s, out int value)
    {
        value = 0;
        s = s.Trim();
        bool ok = s.StartsWith("0x") || s.StartsWith("0X")
            ? int.TryParse(s.AsSpan(2), System.Globalization.NumberStyles.HexNumber, null, out value)
            : int.TryParse(s, out value);
        return ok && value is >= 0 and <= 0xFF;
    }

    // Conservatively over-approximates whether `line` may read register r. Must err
    // toward "yes": any unrecognised instruction is assumed to read r so the
    // park-round-trip pass never reorders a value past a hidden use.
    private static bool ReadsReg(AvrAsmLine line, int r)
    {
        if (line.Type is AvrAsmLine.LineType.Comment or AvrAsmLine.LineType.Empty
                      or AvrAsmLine.LineType.DebugMarker or AvrAsmLine.LineType.Label)
            return false;
        if (line.Type == AvrAsmLine.LineType.Raw)
            return !IsDirectiveLine(line);
        string m = line.Mnemonic;
        int op1 = ParseReg(line.Op1), op2 = ParseReg(line.Op2);
        if (m is "CALL" or "RCALL" or "ICALL" or "EICALL") return r is >= 18 and <= 25;  // PyMCU arg regs
        if (m is "RET" or "RETI") return r is 24 or 25;                                  // return value
        if (m.StartsWith("BR") || m is "RJMP" or "JMP" or "NOP" or "SEI" or "CLI"
            or "WDR" or "SLEEP" or "SEC" or "CLC" or "SEZ" or "CLZ") return false;
        if (m is "LDI" or "CLR" or "SER" or "LDS" or "IN" or "POP") return false;        // pure dest, no GP read
        if (m == "MOV") return op2 == r;
        if (m == "MOVW") return op2 == r || op2 + 1 == r;
        if (m is "LD" or "LDD") return r is >= 26 and <= 31;                             // pointer (X/Y/Z)
        if (m is "LPM" or "ELPM") return r is 30 or 31;                                  // Z
        if (m is "ST" or "STD") return op2 == r || r is >= 26 and <= 31;                 // value + pointer
        if (m is "STS" or "OUT") return op2 == r;                                        // value
        if (m == "PUSH") return op1 == r;
        if (m is "ADIW" or "SBIW") return op1 == r || op1 + 1 == r;
        return op1 == r || op2 == r;   // ALU / compare / shift / unrecognised -> reads its operands
    }

    // A materialized variable-index array address living in Z (R30:R31), as emitted by
    // CompileArrayLoad/Store: an index load into R24, an optional LSL (16-bit scale),
    // then LDI R30,c0; LDI R31,c1; ADD R30,R24; ADC R31,R1. The following LD/ST reads
    // Z without modifying it, so Z stays valid until the index source or Z is touched.
    private readonly struct ZBlock
    {
        public readonly int Start;    // line index of the index load (first line)
        public readonly int End;      // line index of the ADC R31,R1 (last line)
        public readonly int SrcReg;   // index source register (MOV idxload), else -1
        public readonly int SrcSlot;  // index source Y+offset (LDD idxload), else -1
        public readonly string Recipe;// canonical signature: idxload|lsl|c0|c1
        public ZBlock(int start, int end, int srcReg, int srcSlot, string recipe)
            => (Start, End, SrcReg, SrcSlot, Recipe) = (start, end, srcReg, srcSlot, recipe);
    }

    // Parses the 5/6-line Z-address recipe starting at significant line `i`. The index
    // scratch is always R24 (LoadIntoReg(index, "R24")); the LSL is present only for
    // 2-byte elements. Returns false unless the whole canonical shape is present.
    private static bool TryParseZBlock(List<AvrAsmLine> lines, int i, out ZBlock block)
    {
        block = default;
        var idx = lines[i];
        if (idx.Type != AvrAsmLine.LineType.Instruction || idx.Op1 != "R24") return false;
        int srcReg = -1, srcSlot = -1;
        if (idx.Mnemonic == "MOV") { srcReg = ParseReg(idx.Op2); if (srcReg < 0) return false; }
        else if (idx.Mnemonic == "LDD") { srcSlot = ParseYOffset(idx.Op2); if (srcSlot < 0) return false; }
        else return false;

        int j = NextSignificant(lines, i);
        if (j < 0) return false;
        bool hasLsl = lines[j].Type == AvrAsmLine.LineType.Instruction
                      && lines[j].Mnemonic == "LSL" && lines[j].Op1 == "R24";
        if (hasLsl) { j = NextSignificant(lines, j); if (j < 0) return false; }

        var ldi0 = lines[j];
        if (ldi0.Type != AvrAsmLine.LineType.Instruction || ldi0.Mnemonic != "LDI" || ldi0.Op1 != "R30")
            return false;
        int j1 = NextSignificant(lines, j);
        if (j1 < 0) return false;
        var ldi1 = lines[j1];
        if (ldi1.Type != AvrAsmLine.LineType.Instruction || ldi1.Mnemonic != "LDI" || ldi1.Op1 != "R31")
            return false;
        int j2 = NextSignificant(lines, j1);
        if (j2 < 0) return false;
        var add = lines[j2];
        if (add.Type != AvrAsmLine.LineType.Instruction || add.Mnemonic != "ADD"
            || add.Op1 != "R30" || add.Op2 != "R24") return false;
        int j3 = NextSignificant(lines, j2);
        if (j3 < 0) return false;
        var adc = lines[j3];
        if (adc.Type != AvrAsmLine.LineType.Instruction || adc.Mnemonic != "ADC"
            || adc.Op1 != "R31" || adc.Op2 != "R1") return false;

        string recipe = $"{idx.Mnemonic}|{idx.Op2}|{hasLsl}|{ldi0.Op2}|{ldi1.Op2}";
        block = new ZBlock(i, j3, srcReg, srcSlot, recipe);
        return true;
    }

    // True if `line` may invalidate the standing Z address `z`: a control-flow boundary
    // (label/jump/branch/RET, or a CALL — which clobbers Z per WritesReg), a write to Z
    // itself, a write to the index source register, or — for a slot-sourced index — any
    // memory store that might alias the slot. Conservative: the same path-divergence
    // discipline as RegDeadAfter, so a stale Z is never reused across a join.
    private static bool InvalidatesZ(AvrAsmLine line, ZBlock z)
    {
        switch (line.Type)
        {
            case AvrAsmLine.LineType.Label: return true;
            case AvrAsmLine.LineType.Raw: return !IsDirectiveLine(line);
            case AvrAsmLine.LineType.Instruction: break;
            default: return false;
        }
        string m = line.Mnemonic;
        if (m is "RJMP" or "JMP" or "IJMP" or "EIJMP" or "RET" or "RETI" || m.StartsWith("BR"))
            return true;
        if (WritesReg(line, 30) || WritesReg(line, 31)) return true;   // Z corrupted (incl. CALL)
        if (z.SrcReg >= 0 && WritesReg(line, z.SrcReg)) return true;    // index changed
        if (z.SrcSlot >= 0 && m is "STD" or "ST" or "STS") return true; // slot may be overwritten
        return false;
    }

    // Drops the second materialization of an array address that Z already holds. Walks
    // the stream tracking the live Z recipe; when an identical recipe is rebuilt while
    // the index source and Z are provably intact (the walk nulls the fact at any
    // invalidating line), the rebuild — index load, optional LSL, two LDIs, ADD, ADC —
    // is deleted. The following LD/ST keeps using the address still sitting in Z.
    private static void EliminateRedundantZSetup(List<AvrAsmLine> lines, ref bool changed)
    {
        ZBlock? live = null;
        for (int i = 0; i < lines.Count; i++)
        {
            var t = lines[i].Type;
            if (t is AvrAsmLine.LineType.Comment or AvrAsmLine.LineType.Empty
                  or AvrAsmLine.LineType.DebugMarker)
                continue;

            if (TryParseZBlock(lines, i, out ZBlock cur))
            {
                if (live is ZBlock p && p.Recipe == cur.Recipe)
                {
                    for (int k = cur.Start; k <= cur.End; k++)
                        lines[k] = AvrAsmLine.MakeEmpty();
                    int c = cur.Start - 1;          // drop the "...index via Z" comment too
                    if (c >= 0 && lines[c].Type == AvrAsmLine.LineType.Comment)
                        lines[c] = AvrAsmLine.MakeEmpty();
                    changed = true;
                    i = cur.End;                    // Z still holds p's address; keep `live`
                    continue;
                }
                live = cur;
                i = cur.End;
                continue;
            }

            if (live is ZBlock z && InvalidatesZ(lines[i], z))
                live = null;
        }
    }

    // Collapses a park/unpark round-trip the call-argument setup leaves behind:
    //   MOV Rh, Rs ; <ops setting up other args, clobbering Rs> ; MOV Rd, Rh ; CALL
    // becomes
    //   MOV Rd, Rs ; <ops> ; CALL
    // moving the value straight into its final register instead of stashing it in a
    // home and reloading it. Sound only when, between the two MOVs, nothing reads or
    // writes Rh or Rd and there is no call/branch/label (so reordering is safe), and
    // Rh is dead after the unpark (so dropping its definition loses nothing).
    private static void EliminateParkRoundTrip(List<AvrAsmLine> lines, ref bool changed)
    {
        for (int i = 0; i < lines.Count; i++)
        {
            if (lines[i].Type != AvrAsmLine.LineType.Instruction || lines[i].Mnemonic != "MOV")
                continue;
            int rh = ParseReg(lines[i].Op1), rs = ParseReg(lines[i].Op2);   // park: MOV Rh, Rs
            if (rh < 0 || rs < 0 || rh == rs) continue;

            for (int j = i + 1; j < lines.Count; j++)
            {
                var lj = lines[j];
                if (lj.Type is AvrAsmLine.LineType.Comment or AvrAsmLine.LineType.Empty
                            or AvrAsmLine.LineType.DebugMarker) continue;
                if (lj.Type != AvrAsmLine.LineType.Instruction) break;      // label / raw -> bail
                string m = lj.Mnemonic;

                if (m == "MOV" && ParseReg(lj.Op2) == rh)                   // candidate unpark MOV Rd, Rh
                {
                    int rd = ParseReg(lj.Op1);
                    if (rd >= 0 && rd != rh && rd != rs
                        && !RegTouchedBetween(lines, i, j, rd)
                        && RegDeadAfter(lines, j, rh))
                    {
                        lines[i] = AvrAsmLine.MakeInstruction("MOV", "R" + rd, "R" + rs);
                        lines[j] = AvrAsmLine.MakeEmpty();
                        changed = true;
                    }
                    break;   // first unpark candidate decides; stop scanning this park
                }

                // Reordering is unsafe across control flow, and Rh must stay intact.
                if (m is "CALL" or "RCALL" or "ICALL" or "EICALL" or "RET" or "RETI"
                    or "RJMP" or "JMP" || m.StartsWith("BR")) break;
                if (ReadsReg(lj, rh) || WritesReg(lj, rh)) break;
            }
        }
    }

    // True if register r is read or written by any instruction strictly between i and j.
    private static bool RegTouchedBetween(List<AvrAsmLine> lines, int i, int j, int r)
    {
        for (int k = i + 1; k < j; k++)
            if (ReadsReg(lines[k], r) || WritesReg(lines[k], r)) return true;
        return false;
    }

    // True if register r is provably dead after index j: scanning the straight-line
    // continuation, it is redefined (written) before any read. The reasoning only
    // holds without path divergence, so this bails (returns false) at any branch,
    // jump, label, RET, or non-directive raw asm — a write seen past a conditional
    // branch does not redefine r on the not-taken path (e.g. a `min = x` guarded by
    // `BRSH`). A plain CALL is transparent: it returns to the next instruction and,
    // per ReadsReg/WritesReg, only touches the argument/scratch registers.
    private static bool RegDeadAfter(List<AvrAsmLine> lines, int j, int r)
    {
        for (int k = j + 1; k < lines.Count; k++)
        {
            var lk = lines[k];
            if (lk.Type is AvrAsmLine.LineType.Comment or AvrAsmLine.LineType.Empty
                        or AvrAsmLine.LineType.DebugMarker) continue;
            if (lk.Type == AvrAsmLine.LineType.Raw)
            {
                if (IsDirectiveLine(lk)) continue;   // assembler metadata: no register effect
                return false;                        // hand-written asm: unknown effects -> bail
            }
            if (lk.Type == AvrAsmLine.LineType.Label) return false;
            string m = lk.Mnemonic;
            if (m is "RET" or "RETI" or "RJMP" or "JMP" or "IJMP" or "EIJMP" || m.StartsWith("BR"))
                return false;                        // path divergence -> linear reasoning unsound
            if (ReadsReg(lk, r)) return false;       // used before redefinition -> live
            if (WritesReg(lk, r)) return true;       // redefined first -> dead
        }
        return false;
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
            case AvrAsmLine.LineType.Raw:
                return TryRawWordSize(ln.Content, out words);
            default:
                return false;
        }
    }

    // Size a Raw block (e.g. an inline-asm body emitted as one multi-line string) by counting
    // its instruction words. Returns false on anything that emits data or moves the origin
    // (.org/.byte/.word/...) — those we cannot account for in a relative distance, so the
    // candidate spanning them is left untouched.
    private static bool TryRawWordSize(string content, out int words)
    {
        words = 0;
        foreach (var rawLine in content.Split('\n'))
        {
            var line = rawLine;
            int sc = line.IndexOf(';');
            if (sc >= 0) line = line[..sc];
            line = line.Trim();
            if (line.Length == 0 || line.EndsWith(":")) continue;   // blank / label

            string head = line.Split(new[] { ' ', '\t', ',' }, 2,
                                     StringSplitOptions.RemoveEmptyEntries)[0];
            if (head.StartsWith("."))
            {
                switch (head.ToLowerInvariant())
                {
                    case ".equ": case ".set": case ".global": case ".extern":
                    case ".def": case ".undef": case ".list": case ".nolist":
                    case ".cseg": case ".dseg":
                        continue;          // zero-size assembler metadata
                    default:
                        return false;      // .org / data directive / unknown
                }
            }
            words += head.ToUpperInvariant() is "CALL" or "JMP" or "LDS" or "STS" ? 2 : 1;
        }
        return true;
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