// SPDX-License-Identifier: MIT
// pymcuc-avr-pgo-survey — static instruction classification for the AVR corpus.
//
// Decodes each flash word once into a compact per-PC metadata record, so the
// per-instruction profiling callback stays O(1). Only what the survey needs is
// decoded: control flow (targets, sizes), the conditional-branch family, the
// memory-access family with effective-address bases, and I/O reads.

namespace PyMCU.AVR.PgoSurvey;

public enum InsnKind : byte
{
    Other,
    Nop,
    BrCond,     // BRBS/BRBC family (BREQ, BRNE, BRSH, ...) — relative, conditional
    SkipConst,  // SBRC/SBRS — conditional skip over next instruction
    SkipIo,     // SBIC/SBIS — conditional skip on an I/O bit
    Cpse,       // CPSE — conditional skip on register compare
    Rjmp,
    Rcall,
    Call,
    Jmp,
    Ret,
    Reti,
    Lds,        // LDS Rd, k  (2 words) — SRAM/IO read at absolute addr
    Sts,        // STS k, Rr  (2 words) — SRAM/IO write at absolute addr
    LdX, LdY, LdZ,   // includes post-inc / pre-dec and LDD q-forms
    StX, StY, StZ,
    Push,
    Pop,
    In,         // IN Rd, A   — I/O read
    Out,        // OUT A, Rr  — I/O write
    SbiCbi,     // SBI/CBI    — I/O bit write
    Mov,
    Movw,
    Lpm,        // LPM/ELPM   — program-memory read (flash traffic, not SRAM)
    Break,
    HaltJump,   // rjmp .  / jmp .  — self-targeting spin (halt marker)
}

public enum MemBase : byte { None, Abs, X, Y, Z, StackPush, StackPop }

public struct Insn
{
    public InsnKind Kind;
    public byte Words;          // 1 or 2
    public int TakenTarget;     // branch: target word; skip: word after skipped insn; -1 n/a
    public int FallTarget;      // word after this instruction
    public MemBase Base;        // memory ops: effective-address base
    public int Addr;            // LDS/STS: absolute data addr; IN/OUT/SBI*: io addr
    public bool IsMemRead;      // loads (incl POP, IN)
    public bool IsMemWrite;     // stores (incl PUSH, OUT, SBI/CBI)

    public bool IsCondBranch =>
        Kind is InsnKind.BrCond or InsnKind.SkipConst or InsnKind.SkipIo or InsnKind.Cpse;
    public bool IsIoAccess =>
        Kind is InsnKind.In or InsnKind.Out or InsnKind.SkipIo or InsnKind.SbiCbi;

    /// <summary>Opcode-level check (no decode needed): is this word an
    /// arithmetic op on a register that could serve as a loop counter —
    /// DEC/INC/SUB/SBC/SUBI/SBCI/ADIW/SBIW/CPI?</summary>
    public static bool IsCounterOp(ushort op) =>
        (op & 0xFE0F) == 0x940A          // DEC
        || (op & 0xFE0F) == 0x9403       // INC
        || (op & 0xFC00) == 0x1800       // SUB
        || (op & 0xFC00) == 0x0800       // SBC
        || (op & 0xF000) == 0x5000       // SUBI
        || (op & 0xF000) == 0x4000       // SBCI
        || (op & 0xF000) == 0x3000       // CPI
        || (op & 0xFF00) == 0x9600       // ADIW
        || (op & 0xFF00) == 0x9700;      // SBIW

    /// <summary>Word length of the instruction at <paramref name="pc"/>.</summary>
    public static int WordsOf(ushort op)
    {
        if ((op & 0xFE0E) == 0x940C) return 2;          // JMP
        if ((op & 0xFE0E) == 0x940E) return 2;          // CALL
        if ((op & 0xFC0F) == 0x9000) return 2;          // LDS
        if ((op & 0xFC0F) == 0x9200) return 2;          // STS
        return 1;
    }

    private static int SignExtend(int v, int bits) =>
        (v & (1 << (bits - 1))) != 0 ? v - (1 << bits) : v;

    /// <summary>Decode the instruction word at <paramref name="pc"/>.</summary>
    public static Insn Decode(uint pc, ushort op, ushort next)
    {
        var i = new Insn { Kind = InsnKind.Other, Words = 1, TakenTarget = -1,
                           FallTarget = (int)pc + 1, Addr = -1, Base = MemBase.None };

        // ── Control flow ────────────────────────────────────────────────
        if ((op & 0xF000) == 0xC000)                    // RJMP k
        {
            i.Kind = InsnKind.Rjmp;
            var k = SignExtend(op & 0x0FFF, 12);
            i.TakenTarget = (int)(pc + 1 + k);
            if (k == -1) i.Kind = InsnKind.HaltJump;    // rjmp .  — terminal spin
            return i;
        }
        if ((op & 0xF000) == 0xD000)                    // RCALL k
        {
            i.Kind = InsnKind.Rcall;
            i.TakenTarget = (int)(pc + 1 + SignExtend(op & 0x0FFF, 12));
            return i;
        }
        if ((op & 0xFE0E) == 0x940C)                    // JMP k (2 words)
        {
            i.Kind = InsnKind.Jmp; i.Words = 2; i.FallTarget = (int)pc + 2;
            i.TakenTarget = next | ((op & 1) << 16) | ((op & 0x1F0) << 13);
            if (i.TakenTarget == (int)pc) i.Kind = InsnKind.HaltJump;
            return i;
        }
        if ((op & 0xFE0E) == 0x940E)                    // CALL k (2 words)
        {
            i.Kind = InsnKind.Call; i.Words = 2; i.FallTarget = (int)pc + 2;
            i.TakenTarget = next | ((op & 1) << 16) | ((op & 0x1F0) << 13);
            return i;
        }
        if (op == 0x9508) { i.Kind = InsnKind.Ret; return i; }
        if (op == 0x9518) { i.Kind = InsnKind.Reti; return i; }
        if (op == 0x9598) { i.Kind = InsnKind.Break; return i; }
        if (op == 0x0000) { i.Kind = InsnKind.Nop; return i; }

        // ── Conditional branches ────────────────────────────────────────
        if ((op & 0xFC00) == 0xF000 || (op & 0xFC00) == 0xF400)   // BRBC/BRBS
        {
            i.Kind = InsnKind.BrCond;
            var k = SignExtend((op >> 3) & 0x7F, 7);
            i.TakenTarget = (int)(pc + 1 + k);
            return i;
        }
        if ((op & 0xFE08) == 0xFC00) { i.Kind = InsnKind.SkipConst; return i; } // SBRC
        if ((op & 0xFE08) == 0xFE00) { i.Kind = InsnKind.SkipConst; return i; } // SBRS
        if ((op & 0xFF00) == 0x9900 || (op & 0xFF00) == 0x9B00)   // SBIC / SBIS
        {
            i.Kind = InsnKind.SkipIo;
            i.Addr = (op >> 3) & 0x1F;                  // I/O-space address
            i.IsMemRead = true;
            return i;
        }
        if ((op & 0xFC00) == 0x1000) { i.Kind = InsnKind.Cpse; return i; }      // CPSE

        // ── Memory access ───────────────────────────────────────────────
        if ((op & 0xFC0F) == 0x9000)                    // LDS Rd, k (2 words)
        {
            i.Kind = InsnKind.Lds; i.Words = 2; i.FallTarget = (int)pc + 2;
            i.Base = MemBase.Abs; i.Addr = next; i.IsMemRead = true;
            return i;
        }
        if ((op & 0xFC0F) == 0x9200)                    // STS k, Rr (2 words)
        {
            i.Kind = InsnKind.Sts; i.Words = 2; i.FallTarget = (int)pc + 2;
            i.Base = MemBase.Abs; i.Addr = next; i.IsMemWrite = true;
            return i;
        }
        switch (op & 0xFE0F)
        {
            case 0x900F: i.Kind = InsnKind.Pop; i.Base = MemBase.StackPop; i.IsMemRead = true; return i;
            case 0x920F: i.Kind = InsnKind.Push; i.Base = MemBase.StackPush; i.IsMemWrite = true; return i;
            case 0x900C: i.Kind = InsnKind.LdX; i.Base = MemBase.X; i.IsMemRead = true; return i;
            case 0x900D: i.Kind = InsnKind.LdX; i.Base = MemBase.X; i.IsMemRead = true; return i;
            case 0x900E: i.Kind = InsnKind.LdX; i.Base = MemBase.X; i.IsMemRead = true; return i;
            case 0x920C: i.Kind = InsnKind.StX; i.Base = MemBase.X; i.IsMemWrite = true; return i;
            case 0x920D: i.Kind = InsnKind.StX; i.Base = MemBase.X; i.IsMemWrite = true; return i;
            case 0x920E: i.Kind = InsnKind.StX; i.Base = MemBase.X; i.IsMemWrite = true; return i;
            case 0x9009: i.Kind = InsnKind.LdY; i.Base = MemBase.Y; i.IsMemRead = true; return i;
            case 0x900A: i.Kind = InsnKind.LdY; i.Base = MemBase.Y; i.IsMemRead = true; return i;
            case 0x9001: i.Kind = InsnKind.LdZ; i.Base = MemBase.Z; i.IsMemRead = true; return i;
            case 0x9002: i.Kind = InsnKind.LdZ; i.Base = MemBase.Z; i.IsMemRead = true; return i;
            case 0x9209: i.Kind = InsnKind.StY; i.Base = MemBase.Y; i.IsMemWrite = true; return i;
            case 0x920A: i.Kind = InsnKind.StY; i.Base = MemBase.Y; i.IsMemWrite = true; return i;
            case 0x9201: i.Kind = InsnKind.StZ; i.Base = MemBase.Z; i.IsMemWrite = true; return i;
            case 0x9202: i.Kind = InsnKind.StZ; i.Base = MemBase.Z; i.IsMemWrite = true; return i;
            case 0x9004: case 0x9005: i.Kind = InsnKind.Lpm; return i;   // LPM Rd,Z(+)
            case 0x9204: case 0x9205: case 0x9206: case 0x9207:
                // XCH/LAS/LAC/LAT — not on the classic core; treat as Z-store if seen.
                i.Kind = InsnKind.StZ; i.Base = MemBase.Z; i.IsMemWrite = true; return i;
        }
        if (op == 0x95C8 || op == 0x95D8) { i.Kind = InsnKind.Lpm; return i; } // LPM / ELPM

        // ── I/O space ───────────────────────────────────────────────────
        // IN/OUT carry a 6-bit I/O address (A5 sits at bit10), so the mask is
        // 0xF800 (bits 15..11), not 0xFC00 — the whole 0xB000 block is theirs.
        if ((op & 0xF800) == 0xB000)                    // IN Rd, A
        {
            i.Kind = InsnKind.In; i.IsMemRead = true;
            i.Addr = ((op >> 5) & 0x30) | (op & 0x0F);
            return i;
        }
        if ((op & 0xF800) == 0xB800)                    // OUT A, Rr
        {
            i.Kind = InsnKind.Out; i.IsMemWrite = true;
            i.Addr = ((op >> 5) & 0x30) | (op & 0x0F);
            return i;
        }
        if ((op & 0xFF00) == 0x9A00 || (op & 0xFF00) == 0x9800)   // SBI / CBI
        {
            i.Kind = InsnKind.SbiCbi; i.IsMemWrite = true;
            i.Addr = (op >> 3) & 0x1F;
            return i;
        }

        // LDD/STD (Y/Z + q): 10q0 qqsd dddd yqqq — s=1 store, y=1 → Y else Z.
        // Must sit AFTER the I/O tests: under mask 0xD208 an `in` with bit3 of
        // the port number set (e.g. IN Rd, PIND = 0xB009) aliases to LDD Y+q.
        if ((op & 0xD208) == 0x8000 || (op & 0xD208) == 0x8008 ||
            (op & 0xD208) == 0x8200 || (op & 0xD208) == 0x8208)
        {
            bool store = (op & 0x0200) != 0;
            bool yBase = (op & 0x0008) != 0;
            i.Base = yBase ? MemBase.Y : MemBase.Z;
            i.Kind = store ? (yBase ? InsnKind.StY : InsnKind.StZ)
                           : (yBase ? InsnKind.LdY : InsnKind.LdZ);
            var q = ((op >> 8) & 0x20) | ((op >> 7) & 0x18) | (op & 0x07);
            i.Addr = q;                                  // displacement, resolved at exec
            if (store) i.IsMemWrite = true; else i.IsMemRead = true;
            return i;
        }

        if ((op & 0xFC00) == 0x2C00) { i.Kind = InsnKind.Mov; return i; }   // MOV
        if ((op & 0xFF00) == 0x0100) { i.Kind = InsnKind.Movw; return i; }  // MOVW

        return i;
    }
}
