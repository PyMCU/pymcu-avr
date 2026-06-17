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

using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using PyMCU.Backend.Analysis;
using PyMCU.Common.Models;
using PyMCU.IR;
using IrBinOp = PyMCU.IR.BinaryOp;
using IrUnOp = PyMCU.IR.UnaryOp;

namespace PyMCU.Backend.Targets.AVR;

public class AvrCodeGen(DeviceConfig cfg) : CodeGen
{
    private int _loopCounter = 0;
    private readonly List<AvrAsmLine> _assembly = [];
    private Dictionary<string, int> _stackLayout = new();
    private Dictionary<string, int> _varSizes = new();
    private Dictionary<string, string> _regLayout = new();
    private Dictionary<string, string> _tmpRegLayout = new();
    private readonly HashSet<string> _allTmpRegNames = [];
    private readonly HashSet<int> _usedExnCodes = [];
    private HashSet<string> _varIsFloat = new();
    private readonly Dictionary<string, List<int>> _flashArrayPool = new();
    // Maps function name → list of parameter sizes (in bytes) for correct call-site arg loading.
    private Dictionary<string, List<int>> _functionParamSizes = new();
    private int _labelCounter;
    private Function? _currentFunction;
    private int _maxStaticUsage; // total static SRAM used by StackAllocator; set in Compile()
    private int _bssSize;
    private bool _needsGc;      // mirrors program.NeedsGc for use in CompileFunction

    // Divmod fusion (per function): __div16 already produces both quotient (R24:R25)
    // and remainder (R26:R27). When the same dividend is divided AND mod'd by the same
    // divisor in one basic block (the decimal-print digit loop: r = v % 10; v = v // 10),
    // the pair collapses to a single __div16 call. _divModFuse maps the primary Binary
    // (where the call is emitted) to the two result destinations; _divModSkip holds the
    // secondary Binary, which emits nothing. Reference identity, rebuilt each function.
    private Dictionary<Binary, (Val Quot, Val Rem)> _divModFuse =
        new(ReferenceEqualityComparer.Instance);
    private HashSet<Binary> _divModSkip = new(ReferenceEqualityComparer.Instance);

    // Byte-pack fusion (per function): reconstructing a 16-bit value from two bytes --
    // result = (hi << 8) | lo  (or + lo) -- is just "low byte = lo, high byte = hi", but
    // the generic widen+shift+add lowering spends ~10 instructions on it. Every 16-bit
    // sensor read (ADC ADCH/ADCL, BMP280, DHT, ...) hits this. _bytePackFuse maps the
    // combining Binary (Add/BitOr, where the packed value is emitted) to the (hi, lo)
    // byte sources; _bytePackSkip holds the now-dead `hi << 8` shift.
    private Dictionary<Binary, (Val Hi, Val Lo)> _bytePackFuse =
        new(ReferenceEqualityComparer.Instance);
    private HashSet<Binary> _bytePackSkip = new(ReferenceEqualityComparer.Instance);

    private string MakeLabel(string prefix = ".L") => $"{prefix}_{_labelCounter++}";
    private static string GetHighReg(string reg) => "R" + (int.Parse(reg[1..]) + 1);

    // If the value is resident in an allocated home register (named-var R4-R15 pool or
    // R16/R17 temporary pool), return that register's low byte; otherwise null. Used by
    // reg-reg ALU emitters to consume the operand directly instead of staging it through
    // R18. Reading a register as the second ALU operand never clobbers it, so this is
    // safe for both pools.
    private string? OperandHomeReg(Val v)
    {
        var name = v switch { Variable x => x.Name, Temporary t => t.Name, _ => null };
        if (name == null) return null;
        if (_regLayout.TryGetValue(name, out var r)) return r;
        if (_tmpRegLayout.TryGetValue(name, out var r2)) return r2;
        return null;
    }
    private void Emit(string m) => _assembly.Add(AvrAsmLine.MakeInstruction(m));
    private void Emit(string m, string o1) => _assembly.Add(AvrAsmLine.MakeInstruction(m, o1));
    private void Emit(string m, string o1, string o2) => _assembly.Add(AvrAsmLine.MakeInstruction(m, o1, o2));
    private void EmitLabel(string l) => _assembly.Add(AvrAsmLine.MakeLabel(l));
    private void EmitComment(string c) => _assembly.Add(AvrAsmLine.MakeComment(c));
    private void EmitRaw(string t) => _assembly.Add(AvrAsmLine.MakeRaw(t));

    private static string ResolveAddress(Val val)
    {
        switch (val)
        {
            case Constant c:
                return $"{c.Value & 0xFF}";
            case MemoryAddress mem:
                return $"0x{mem.Address:X4}";
            default:
            {
                var name = val switch { Variable v => v.Name, Temporary t => t.Name, _ => "" };
                return name.Replace('.', '_');
            }
        }
    }

    private static DataType GetValType(Val val) => val switch
    {
        FloatConstant => DataType.FLOAT,
        Variable v => v.Type,
        Temporary t => t.Type,
        MemoryAddress m => m.Type.SizeOf() > 1 ? m.Type : DataType.UINT8,
        FunctionRef => DataType.UINT16,  // Function word address is 2 bytes
        Constant { Value: > 255 or < -128 } => DataType.UINT16,
        _ => DataType.UINT8,
    };

    // -------------------------------------------------------------------------
    // Soft-float helpers
    // -------------------------------------------------------------------------

    // Load a FLOAT value into R22(B0/LSB):R23(B1):R24(B2):R25(B3/MSB).
    private void LoadFloatIntoRegs(Val val)
    {
        if (val is FloatConstant fc)
        {
            uint bits = BitConverter.SingleToUInt32Bits((float)fc.Value);
            Emit("LDI", "R22", $"{bits & 0xFF}");
            Emit("LDI", "R23", $"{(bits >> 8) & 0xFF}");
            Emit("LDI", "R24", $"{(bits >> 16) & 0xFF}");
            Emit("LDI", "R25", $"{(bits >> 24) & 0xFF}");
            return;
        }

        // If the source is an integer type, load it as int and convert to float.
        DataType srcType = GetValType(val);
        if (srcType != DataType.FLOAT)
        {
            // GCC __floatsisf: input int32 in R25:R24:R23:R22 (R22=LSB), result float in R25:R24:R23:R22.
            if (srcType.SizeOf() == 1)
            {
                LoadIntoReg(val, "R22", DataType.UINT8);
                if (IsSignedType(srcType))
                {
                    Emit("MOV", "R23", "R22");
                    Emit("LSL", "R23");
                    Emit("SBC", "R23", "R23"); // sign-extend: R23 = 0xFF if negative, 0x00 if positive
                    Emit("MOV", "R24", "R23");
                    Emit("MOV", "R25", "R23");
                }
                else
                {
                    Emit("CLR", "R23");
                    Emit("CLR", "R24");
                    Emit("CLR", "R25");
                }
            }
            else
            {
                // uint16 or int16: load into R22 (lo) : R23 (hi), sign/zero-extend R24:R25.
                LoadIntoReg(val, "R22", DataType.UINT16);
                if (IsSignedType(srcType))
                {
                    Emit("MOV", "R24", "R23");
                    Emit("LSL", "R24");
                    Emit("SBC", "R24", "R24"); // sign-extend: R24 = 0xFF if negative, 0x00 if positive
                    Emit("MOV", "R25", "R24");
                }
                else
                {
                    Emit("CLR", "R24");
                    Emit("CLR", "R25");
                }
            }
            Emit("CALL", "__floatsisf");
            return;
        }

        var name = val switch { Variable v => v.Name, Temporary t => t.Name, _ => "" };

        // If the variable is register-allocated, load from those registers.
        if (!string.IsNullOrEmpty(name) && _regLayout.TryGetValue(name, out string? regBase))
        {
            int rn = int.Parse(regBase[1..]);
            Emit("MOV", "R22", $"R{rn}");
            Emit("MOV", "R23", $"R{rn + 1}");
            Emit("MOV", "R24", $"R{rn + 2}");
            Emit("MOV", "R25", $"R{rn + 3}");
            return;
        }

        if (!string.IsNullOrEmpty(name) && _stackLayout.TryGetValue(name, out int offset))
        {
            if (offset + 3 < 64)
            {
                Emit("LDD", "R22", $"Y+{offset}");
                Emit("LDD", "R23", $"Y+{offset + 1}");
                Emit("LDD", "R24", $"Y+{offset + 2}");
                Emit("LDD", "R25", $"Y+{offset + 3}");
            }
            else
            {
                int abs = 0x0100 + offset;
                Emit("LDS", "R22", $"0x{abs:X4}");
                Emit("LDS", "R23", $"0x{abs + 1:X4}");
                Emit("LDS", "R24", $"0x{abs + 2:X4}");
                Emit("LDS", "R25", $"0x{abs + 3:X4}");
            }
            return;
        }

        // Global variable fallback
        var addr = ResolveAddress(val);
        Emit("LDS", "R22", addr);
        Emit("LDS", "R23", $"{addr}+1");
        Emit("LDS", "R24", $"{addr}+2");
        Emit("LDS", "R25", $"{addr}+3");
    }

    // Store R22:R23:R24:R25 into a FLOAT destination.
    private void StoreFloatFromRegs(Val dst)
    {
        var name = dst switch { Variable v => v.Name, Temporary t => t.Name, _ => "" };

        // If the destination is register-allocated, move into those registers.
        if (!string.IsNullOrEmpty(name) && _regLayout.TryGetValue(name, out string? regBase))
        {
            int rn = int.Parse(regBase[1..]);
            Emit("MOV", $"R{rn}", "R22");
            Emit("MOV", $"R{rn + 1}", "R23");
            Emit("MOV", $"R{rn + 2}", "R24");
            Emit("MOV", $"R{rn + 3}", "R25");
            return;
        }

        if (!string.IsNullOrEmpty(name) && _stackLayout.TryGetValue(name, out int offset))
        {
            if (offset + 3 < 64)
            {
                Emit("STD", $"Y+{offset}", "R22");
                Emit("STD", $"Y+{offset + 1}", "R23");
                Emit("STD", $"Y+{offset + 2}", "R24");
                Emit("STD", $"Y+{offset + 3}", "R25");
            }
            else
            {
                int abs = 0x0100 + offset;
                Emit("STS", $"0x{abs:X4}", "R22");
                Emit("STS", $"0x{abs + 1:X4}", "R23");
                Emit("STS", $"0x{abs + 2:X4}", "R24");
                Emit("STS", $"0x{abs + 3:X4}", "R25");
            }
            return;
        }

        var addr = ResolveAddress(dst);
        Emit("STS", addr, "R22");
        Emit("STS", $"{addr}+1", "R23");
        Emit("STS", $"{addr}+2", "R24");
        Emit("STS", $"{addr}+3", "R25");
    }

    // Compile a binary operation where at least one operand is FLOAT.
    private void CompileFloatBinary(Binary b)
    {
        // Load arg0 (Src1) into R22:R25, push onto stack, load arg1 (Src2) into R22:R25,
        // move arg1 to R18:R21 (GCC arg1 slot), restore arg0 to R22:R25 (GCC arg0 slot).
        // GCC AVR float ABI: arg0 in R25:R24:R23:R22, arg1 in R21:R20:R19:R18, result in R25:R24:R23:R22.
        LoadFloatIntoRegs(b.Src1);
        Emit("PUSH", "R25");
        Emit("PUSH", "R24");
        Emit("PUSH", "R23");
        Emit("PUSH", "R22");
        LoadFloatIntoRegs(b.Src2);
        Emit("MOV", "R18", "R22");
        Emit("MOV", "R19", "R23");
        Emit("MOV", "R20", "R24");
        Emit("MOV", "R21", "R25");
        Emit("POP", "R22");
        Emit("POP", "R23");
        Emit("POP", "R24");
        Emit("POP", "R25");

        bool isArith = b.Op is IrBinOp.Add or IrBinOp.Sub or IrBinOp.Mul
                       or IrBinOp.Div or IrBinOp.FloorDiv;
        if (isArith)
        {
            string routine = b.Op switch
            {
                IrBinOp.Add                     => "__addsf3",
                IrBinOp.Sub                     => "__subsf3",
                IrBinOp.Mul                     => "__mulsf3",
                IrBinOp.Div or IrBinOp.FloorDiv => "__divsf3",
                _ => throw new NotSupportedException($"Float arith op {b.Op} not supported")
            };
            Emit("CALL", routine);
            var dstType = GetValType(b.Dst);
            if (dstType == DataType.FLOAT)
                StoreFloatFromRegs(b.Dst);
            else
            {
                // Result is float in R22:R25; __fixsfsi converts to int32 (R22=LSB).
                Emit("CALL", "__fixsfsi");
                Emit("MOV", "R24", "R22");
                Emit("MOV", "R25", "R23");
                StoreRegInto("R24", b.Dst, dstType);
            }
        }
        else
        {
            // Float comparison via GCC __cmpsf2.
            // Returns in R24: 0xFF if arg0<arg1, 0x00 if arg0==arg1, 0x01 if arg0>arg1.
            Emit("CALL", "__cmpsf2");
            string trueLabel = MakeLabel("L_FCMP_T");
            string doneLabel = MakeLabel("L_FCMP_D");
            switch (b.Op)
            {
                case IrBinOp.Equal:        // true when R24 == 0x00
                    Emit("CPI", "R24", "0x00"); Emit("BREQ", trueLabel); break;
                case IrBinOp.NotEqual:     // true when R24 != 0x00
                    Emit("CPI", "R24", "0x00"); Emit("BRNE", trueLabel); break;
                case IrBinOp.LessThan:     // true when R24 == 0xFF (a < b)
                    Emit("CPI", "R24", "0xFF"); Emit("BREQ", trueLabel); break;
                case IrBinOp.LessEqual:    // true when R24 != 0x01 (covers 0x00 and 0xFF)
                    Emit("CPI", "R24", "0x01"); Emit("BRNE", trueLabel); break;
                case IrBinOp.GreaterThan:  // true when R24 == 0x01 (a > b)
                    Emit("CPI", "R24", "0x01"); Emit("BREQ", trueLabel); break;
                case IrBinOp.GreaterEqual: // true when R24 != 0xFF (covers 0x00 and 0x01)
                    Emit("CPI", "R24", "0xFF"); Emit("BRNE", trueLabel); break;
                default: throw new NotSupportedException($"Float comparison op {b.Op} not supported");
            }
            Emit("CLR", "R24");
            Emit("RJMP", doneLabel);
            EmitLabel(trueLabel);
            Emit("LDI", "R24", "1");
            EmitLabel(doneLabel);
            StoreRegInto("R24", b.Dst, DataType.UINT8);
        }
    }

    private static bool IsSignedType(DataType t) => t.IsSigned();

    // Returns true if the comparison should use signed branches (BRLT/BRGE).
    // Negative constants indicate a signed context even when type info is lost by folding.
    private static bool IsSignedComparison(Val src1, Val src2)
    {
        if (IsSignedType(GetValType(src1)) || IsSignedType(GetValType(src2))) return true;
        if (src1 is Constant c1 && c1.Value < 0) return true;
        if (src2 is Constant c2 && c2.Value < 0) return true;
        return false;
    }

    // Picks the division/modulo runtime routine. When `signed` is set, the floor
    // variant (__divs*/__mods*, Python semantics: quotient floors toward -inf and
    // the remainder takes the sign of the divisor) is used; otherwise the unsigned
    // core (__div*/__mod*). `wantMod` selects the modulo entry point.
    private static string DivModRoutine(bool wantMod, bool is32, bool is16, bool signed)
    {
        string sz = is32 ? "32" : is16 ? "16" : "8";
        return $"__{(wantMod ? "mod" : "div")}{(signed ? "s" : "")}{sz}";
    }

    private void EmitBranch(string cond, string target)
    {
        var inv = new Dictionary<string, string>
        {
            { "BREQ", "BRNE" }, { "BRNE", "BREQ" }, { "BRLT", "BRGE" }, { "BRGE", "BRLT" },
            { "BRCS", "BRCC" }, { "BRCC", "BRCS" }, { "BRLO", "BRSH" }, { "BRSH", "BRLO" },
            { "BRTS", "BRTC" }, { "BRTC", "BRTS" },  // T-flag branches (error propagation)
        };
        string inverted = inv.GetValueOrDefault(cond, cond);
        string skip = MakeLabel("L_BR_SKIP");
        Emit(inverted, skip);
        Emit("RJMP", target);
        EmitLabel(skip);
    }

    // Integer-argument register assignment. Each argument takes a 2-register slot (1- or 2-byte
    // values) or a 4-register block (32-bit), allocated from R25 downward so a wide argument no
    // longer collides with a previous one (the old fixed [R24,R22,R20,R18] spacing assumed every
    // arg was <= 2 bytes, so `f(u8, u32)` loaded the u32 into R22:R23:R24:R25 and clobbered arg0).
    // A 32-bit FIRST arg keeps the R24-anchored PyMCU layout (byte0=R24, byte2=R22); a lower 32-bit
    // arg is contiguous from its base. Returns the base register (as passed to LoadIntoReg) per arg.
    // Caller and callee must use this identically. Floats keep their own special-casing.
    private static List<string> ArgBaseRegs(IReadOnlyList<int> sizes)
    {
        var bases = new List<string>(sizes.Count);
        int top = 25;
        foreach (int sz in sizes)
        {
            int slots = sz >= 4 ? 4 : 2;
            int low = top - slots + 1;
            int baseNum = slots == 4 && top == 25 ? 24 : low;
            bases.Add("R" + baseNum);
            top = low - 1;
        }

        return bases;
    }

    private void LoadIntoReg(Val val, string reg, DataType type = DataType.UINT8)
    {
        int size = type.SizeOf();
        var regH  = size >= 2 ? GetHighReg(reg) : "";
        // For 32-bit: byte2=R22, byte3=R23 (AVR-GCC uint32 convention when base=R24)
        // When base is not R24, fall back to reg+2/+3 (not used for 32-bit in practice)
        var regB2 = size == 4 ? (reg == "R24" ? "R22" : $"R{int.Parse(reg[1..]) + 2}") : "";
        var regB3 = size == 4 ? (reg == "R24" ? "R23" : $"R{int.Parse(reg[1..]) + 3}") : "";

        switch (val)
        {
            case ArrayBase ab:
            {
                string regH2 = GetHighReg(reg);
                // Try the array base name first (registered via ArrayStore), then fall back
                // to the first element "__0" (registered via individual Copy instructions).
                int abOffset;
                bool found = _stackLayout.TryGetValue(ab.ArrayName, out abOffset)
                          || _stackLayout.TryGetValue(ab.ArrayName + "__0", out abOffset);
                if (found) {
                    int absAddr = 0x0100 + abOffset;
                    Emit("LDI", reg,   $"lo8(0x{absAddr:X4})");
                    Emit("LDI", regH2, $"hi8(0x{absAddr:X4})");
                } else {
                    string label = ab.ArrayName.Replace('.', '_');
                    Emit("LDI", reg,   $"lo8({label})");
                    Emit("LDI", regH2, $"hi8({label})");
                }
                return;
            }
            case Constant c:
            {
                Emit("LDI", reg, $"{c.Value & 0xFF}");
                if (size >= 2) Emit("LDI", regH, $"{(c.Value >> 8) & 0xFF}");
                if (size == 4) { Emit("LDI", regB2, $"{(c.Value >> 16) & 0xFF}"); Emit("LDI", regB3, $"{(c.Value >> 24) & 0xFF}"); }
                return;
            }
            case FunctionRef fr:
            {
                // Load function word address (gs() modifier) into register pair.
                Emit("LDI", reg, $"lo8(gs({fr.FunctionName}))");
                if (size >= 2) Emit("LDI", regH, $"hi8(gs({fr.FunctionName}))");
                return;
            }
            case FlashStrAddr fs:
            {
                // 16-bit flash BYTE address of an interned string (FlashData label is
                // "__flash_" + name, same convention as ArrayLoadFlash). Loaded byte-wise
                // via LPM by FlashLoadPtr, so this is a byte address (no gs()/word scaling).
                string fsLabel = "__flash_" + fs.Name.Replace('.', '_');
                Emit("LDI", reg, $"lo8({fsLabel})");
                if (size >= 2) Emit("LDI", regH, $"hi8({fsLabel})");
                return;
            }
            case MemoryAddress mem:
            {
                if (mem.Address is >= 0x20 and <= 0x5F)
                    Emit("IN", reg, $"0x{mem.Address - 0x20:X2}");
                else
                    Emit("LDS", reg, $"0x{mem.Address:X4}");
                if (size >= 2) Emit("LDS", regH,  $"0x{mem.Address + 1:X4}");
                if (size == 4) { Emit("LDS", regB2, $"0x{mem.Address + 2:X4}"); Emit("LDS", regB3, $"0x{mem.Address + 3:X4}"); }
                return;
            }
        }

        var name = val switch { Variable v2 => v2.Name, Temporary t2 => t2.Name, _ => "" };

        // "__exn_r22_capture" is a register alias: it represents R22 at catch-dispatcher
        // entry — the physical register where SignalError deposited the error code.
        // Compiled as MOV reg, R22 (zero SRAM cost, no stack-overlay risk).
        if (name == "__exn_r22_capture")
        {
            if (reg != "R22") Emit("MOV", reg, "R22");
            return;
        }

        if (!string.IsNullOrEmpty(name) && _regLayout.TryGetValue(name, out var srcReg))
        {
            DataType sourceType = GetValType(val);
            bool needSignExt = size == 2 && sourceType.SizeOf() == 1 && IsSignedType(sourceType);
            bool needZeroExt = size > sourceType.SizeOf() && !IsSignedType(sourceType) && !needSignExt;

            if (srcReg != reg) Emit("MOV", reg, srcReg);
            else if (!needSignExt && !needZeroExt && srcReg == reg)
            {
                // Source already in target reg; still need to populate high bytes if multi-byte
                if (size >= 2) Emit("MOV", regH, GetHighReg(srcReg));
                if (size == 4) { Emit("MOV", regB2, $"R{int.Parse(srcReg[1..]) + 2}"); Emit("MOV", regB3, $"R{int.Parse(srcReg[1..]) + 3}"); }
                return;
            }

            if (needZeroExt)
            {
                if (size >= 2) Emit("CLR", regH);
                if (size == 4) { Emit("CLR", regB2); Emit("CLR", regB3); }
            }
            else
            {
                if (size >= 2 && !needSignExt) Emit("MOV", regH, GetHighReg(srcReg));
                if (size == 4) { Emit("MOV", regB2, $"R{int.Parse(srcReg[1..]) + 2}"); Emit("MOV", regB3, $"R{int.Parse(srcReg[1..]) + 3}"); }
            }

            if (needSignExt)
            {
                Emit("MOV", regH, reg);
                Emit("LSL", regH);
                Emit("SBC", regH, regH);
            }
            return;
        }

        if (!string.IsNullOrEmpty(name) && _tmpRegLayout.TryGetValue(name, out var tmpReg))
        {
            DataType sourceType = GetValType(val);
            bool needSignExt = size == 2 && sourceType.SizeOf() == 1 && IsSignedType(sourceType);
            bool needZeroExt = size > sourceType.SizeOf() && !IsSignedType(sourceType) && !needSignExt;

            if (tmpReg != reg) Emit("MOV", reg, tmpReg);
            if (needZeroExt)
            {
                if (size >= 2) Emit("CLR", regH);
                if (size == 4) { Emit("CLR", regB2); Emit("CLR", regB3); }
            }
            else
            {
                if (size >= 2 && !needSignExt) Emit("MOV", regH, GetHighReg(tmpReg));
                if (size == 4) { Emit("MOV", regB2, $"R{int.Parse(tmpReg[1..]) + 2}"); Emit("MOV", regB3, $"R{int.Parse(tmpReg[1..]) + 3}"); }
            }

            if (needSignExt)
            {
                Emit("MOV", regH, reg);
                Emit("LSL", regH);
                Emit("SBC", regH, regH);
            }
            return;
        }

        if (!string.IsNullOrEmpty(name) && _stackLayout.TryGetValue(name, out int offset))
        {
            bool nearY = offset + (size - 1) < 64;
            DataType sourceType = GetValType(val);
            bool needSignExt = size == 2 && sourceType.SizeOf() == 1 && IsSignedType(sourceType);
            bool needZeroExt = size > sourceType.SizeOf() && !IsSignedType(sourceType) && !needSignExt;

            if (nearY)
            {
                Emit("LDD", reg, $"Y+{offset}");
                if (needZeroExt)
                {
                    if (size >= 2) Emit("CLR", regH);
                    if (size == 4) { Emit("CLR", regB2); Emit("CLR", regB3); }
                }
                else
                {
                    if (size >= 2 && !needSignExt) Emit("LDD", regH,  $"Y+{offset + 1}");
                    if (size == 4) { Emit("LDD", regB2, $"Y+{offset + 2}"); Emit("LDD", regB3, $"Y+{offset + 3}"); }
                }
            }
            else
            {
                var abs = 0x0100 + offset;
                Emit("LDS", reg, $"0x{abs:X4}");
                if (needZeroExt)
                {
                    if (size >= 2) Emit("CLR", regH);
                    if (size == 4) { Emit("CLR", regB2); Emit("CLR", regB3); }
                }
                else
                {
                    if (size >= 2 && !needSignExt) Emit("LDS", regH,  $"0x{abs + 1:X4}");
                    if (size == 4) { Emit("LDS", regB2, $"0x{abs + 2:X4}"); Emit("LDS", regB3, $"0x{abs + 3:X4}"); }
                }
            }

            if (needSignExt)
            {
                Emit("MOV", regH, reg);
                Emit("LSL", regH);
                Emit("SBC", regH, regH);
            }
            return;
        }

        var addr = ResolveAddress(val);
        if (string.IsNullOrEmpty(addr)) return;
        DataType srcType = GetValType(val);
        bool signExt = size == 2 && srcType.SizeOf() == 1 && IsSignedType(srcType);
        bool zeroExt = size > srcType.SizeOf() && !IsSignedType(srcType) && !signExt;

        Emit("LDS", reg, addr);
        if (zeroExt)
        {
            if (size >= 2) Emit("CLR", regH);
            if (size == 4) { Emit("CLR", regB2); Emit("CLR", regB3); }
        }
        else
        {
            if (size >= 2 && !signExt) Emit("LDS", regH, addr + "+1");
            if (size == 4) { Emit("LDS", regB2, addr + "+2"); Emit("LDS", regB3, addr + "+3"); }
        }

        if (signExt)
        {
            Emit("MOV", regH, reg);
            Emit("LSL", regH);
            Emit("SBC", regH, regH);
        }
    }

    private void StoreRegInto(string reg, Val val, DataType type = DataType.UINT8)
    {
        if (val is Constant) return;
        int size = type.SizeOf();
        var regH  = size >= 2 ? GetHighReg(reg) : "";
        var regB2 = size == 4 ? (reg == "R24" ? "R22" : $"R{int.Parse(reg[1..]) + 2}") : "";
        var regB3 = size == 4 ? (reg == "R24" ? "R23" : $"R{int.Parse(reg[1..]) + 3}") : "";

        if (val is MemoryAddress mem)
        {
            if (mem.Address is >= 0x20 and <= 0x5F)
                Emit("OUT", $"0x{mem.Address - 0x20:X2}", reg);
            else
                Emit("STS", $"0x{mem.Address:X4}", reg);
            if (size >= 2) Emit("STS", $"0x{mem.Address + 1:X4}", regH);
            if (size == 4) { Emit("STS", $"0x{mem.Address + 2:X4}", regB2); Emit("STS", $"0x{mem.Address + 3:X4}", regB3); }
            return;
        }

        var name = val switch { Variable v => v.Name, Temporary t => t.Name, _ => "" };

        if (!string.IsNullOrEmpty(name) && _regLayout.TryGetValue(name, out var dstReg))
        {
            if (dstReg != reg) Emit("MOV", dstReg, reg);
            if (size >= 2) Emit("MOV", GetHighReg(dstReg), regH);
            if (size == 4) { Emit("MOV", $"R{int.Parse(dstReg[1..]) + 2}", regB2); Emit("MOV", $"R{int.Parse(dstReg[1..]) + 3}", regB3); }
            return;
        }

        if (!string.IsNullOrEmpty(name) && _tmpRegLayout.TryGetValue(name, out var tmpReg))
        {
            if (tmpReg != reg) Emit("MOV", tmpReg, reg);
            if (size >= 2) Emit("MOV", GetHighReg(tmpReg), regH);
            if (size == 4) { Emit("MOV", $"R{int.Parse(tmpReg[1..]) + 2}", regB2); Emit("MOV", $"R{int.Parse(tmpReg[1..]) + 3}", regB3); }
            return;
        }

        if (!string.IsNullOrEmpty(name) && _stackLayout.TryGetValue(name, out int offset))
        {
            bool nearY = offset + (size - 1) < 64;
            if (nearY)
            {
                Emit("STD", $"Y+{offset}", reg);
                if (size >= 2) Emit("STD", $"Y+{offset + 1}", regH);
                if (size == 4) { Emit("STD", $"Y+{offset + 2}", regB2); Emit("STD", $"Y+{offset + 3}", regB3); }
            }
            else
            {
                var abs = 0x0100 + offset;
                Emit("STS", $"0x{abs:X4}", reg);
                if (size >= 2) Emit("STS", $"0x{abs + 1:X4}", regH);
                if (size == 4) { Emit("STS", $"0x{abs + 2:X4}", regB2); Emit("STS", $"0x{abs + 3:X4}", regB3); }
            }
            return;
        }

        var addr = ResolveAddress(val);
        if (string.IsNullOrEmpty(addr)) return;
        Emit("STS", addr, reg);
        if (size >= 2) Emit("STS", addr + "+1", regH);
        if (size == 4) { Emit("STS", addr + "+2", regB2); Emit("STS", addr + "+3", regB3); }
    }

    public override void Compile(ProgramIR program, TextWriter output)
    {
        _assembly.Clear();
        _flashArrayPool.Clear();
        _allTmpRegNames.Clear();
        _labelCounter = 0;

        // Promote ISR-shared single-byte globals to GPIORn before any allocation
        // pass runs: promoted names become MemoryAddress operands, so the stack
        // and register allocators never see them and BSS shrinks accordingly.
        var gpiorPromotions = AvrGpiorPromotion.Apply(program, cfg);

        var allocator = new StackAllocator();
        var (offsets, maxStack) = allocator.Allocate(program);
        _stackLayout = offsets;
        _maxStaticUsage = maxStack;
        _needsGc = program.NeedsGc;
        _varSizes = allocator.VariableSizes;
        _bssSize = program.Globals.Sum(g => g.Type.SizeOf()) + program.GlobalArrays.Values.Sum();
        _regLayout = AvrRegisterAllocator.Allocate(program);

        // Build set of float-typed variable names for correct register assignment.
        _varIsFloat = [];
        foreach (var func in program.Functions)
            foreach (var instr in func.Body)
            {
                var valsToCheck = instr switch
                {
                    Binary b => new[] { b.Src1, b.Src2, b.Dst },
                    Copy c => new[] { c.Src, c.Dst },
                    Return { Value: not null } r => new[] { r.Value! },
                    Call cl => [.. cl.Args, cl.Dst],
                    _ => Array.Empty<Val>()
                };
                foreach (var v in valsToCheck)
                {
                    if (v is Variable vv && vv.Type == DataType.FLOAT) _varIsFloat.Add(vv.Name);
                    if (v is Temporary tt && tt.Type == DataType.FLOAT) _varIsFloat.Add(tt.Name);
                }
            }

        // Build function parameter size map for correct call-site arg loading.
        _functionParamSizes.Clear();
        foreach (var func in program.Functions)
        {
            var sizes = new List<int>();
            foreach (var p in func.Params)
                sizes.Add(_varSizes.TryGetValue(p, out int sz) ? sz : 1);
            _functionParamSizes[func.Name] = sizes;
        }

        EmitComment("Generated by pymcuc for " + cfg.Chip);

        foreach (var (gName, gAddr) in gpiorPromotions.OrderBy(kv => kv.Value))
            EmitComment($"volatile '{gName}' -> GPIOR @ 0x{gAddr:X2} (ISR-shared, auto-promoted)");

        foreach (var sym in program.ExternSymbols)
            EmitRaw(".extern " + sym);
        if (program.ExternSymbols.Count > 0) EmitRaw("");

        EmitRaw(".equ RAMSTART, 0x0100");
        EmitRaw(".equ _stack_base, RAMSTART");

        foreach (var (name, offset) in _stackLayout)
        {
            if (_regLayout.ContainsKey(name)) continue;
            if (_allTmpRegNames.Contains(name)) continue;
            var safeName = name.Replace('.', '_');
            EmitRaw($".equ {safeName}, _stack_base + {offset}");
        }

        if (_bssSize > 0)
            EmitRaw($".equ _bss_end, _stack_base + {_bssSize}");

        if (program.NeedsGc)
            EmitGcSramLayout();

        EmitRaw("");

        // ISR map
        var isrMap = new SortedDictionary<int, Function>();
        foreach (var func in program.Functions.Where(func => func.IsInterrupt))
        {
            // The vector is a byte address into the table emitted below (vec*2 for vec in
            // 1..25, i.e. the even addresses 0x02..0x32). A vector outside that range — or
            // an odd one — would never match a table slot, so the handler was silently
            // dropped (it ran as dead code, the vector jumped to __bad_interrupt). Reject it.
            if (func.InterruptVector < 2 || func.InterruptVector > 50 || (func.InterruptVector & 1) != 0)
            {
                throw new Exception(
                    $"@interrupt vector 0x{func.InterruptVector:X} on '{func.Name}' is out of range; " +
                    "expected an even vector address in 0x02..0x32 (e.g. 0x04 for INT0, 0x20 for TIMER0_OVF)");
            }

            // Add duplicate ISR check that was missing in the C# port
            if (!isrMap.TryAdd(func.InterruptVector, func))
            {
                throw new Exception($"Multiple ISRs defined for vector 0x{func.InterruptVector:X4}");
            }
        }

        EmitRaw(".org 0x0000");
        EmitRaw(".global main");
        Emit("RJMP", "main");

        // Always emit the vector table. Unused vectors jump to __bad_interrupt which
        // performs a soft reset, matching avr-libc safety semantics.
        // AVR8Sharp sets cpu.Pc = overflowInterrupt (a byte address on real hardware,
        // e.g. 0x12 for Timer2 OVF). Since ProgramMemory is word-indexed, cpu.Pc=0x12
        // executes from byte 0x24 (= 2 × 0x12).
        // AVRA .org uses WORD addresses, and _avra_to_gnuas() multiplies by 2:
        //   AVRA .org 0x0012 → avr-as .org 0x0024 (byte).
        // To place RJMP at byte 0x0024, we need AVRA .org = 0x0012 = vec*2.
        // This matches overflowInterrupt = vec*2 (the byte address on real hardware).
        for (var vec = 1; vec <= 25; vec++)
        {
            EmitRaw($".org 0x{vec * 2:X4}");

            if (isrMap.TryGetValue(vec * 2, out var isrFunc))
            {
                Emit("RJMP", isrFunc.Name);
            }
            else
            {
                Emit("RJMP", "__bad_interrupt");
            }
        }

        EmitRaw("");
        EmitLabel("__bad_interrupt");
        Emit("RJMP", "main");
        EmitRaw("");

        // Emit flash-resident vtables for classes that still require virtual dispatch
        // after the devirtualization pass (empty for the vast majority of programs).
        EmitVtables(program);

        foreach (var func in program.Functions.Where(func => func.IsInterrupt))
            CompileFunction(func);

        // --- Call Graph Analysis for DCE ---
        var referencedFuncs = new HashSet<string>();
        var worklist = new Queue<string>();
        
        void AddRef(string name)
        {
            if (referencedFuncs.Add(name))
                worklist.Enqueue(name);
        }

        AddRef("main");
        foreach (var f in program.Functions.Where(f => f.IsInterrupt))
            AddRef(f.Name);
        foreach (var sym in program.ExternSymbols)
            AddRef(sym);

        while (worklist.Count > 0)
        {
            var fName = worklist.Dequeue();
            var f = program.Functions.FirstOrDefault(x => x.Name == fName);
            if (f == null) continue;
            foreach (var instr in f.Body)
            {
                if (instr is Call c)
                {
                    if ((c.FunctionName == "_delay_ms_avr" || c.FunctionName.EndsWith("__delay_ms_avr")) 
                        && c.Args.Count == 1 && c.Args[0] is Constant msConst)
                    {
                        ulong cycles = (ulong)msConst.Value * (cfg.Frequency / 1000);
                        ulong loops = cycles / 6;
                        if (loops > 0) continue; 
                    }
                    if ((c.FunctionName == "_delay_us_avr" || c.FunctionName.EndsWith("__delay_us_avr")) 
                        && c.Args.Count == 1 && c.Args[0] is Constant usConst)
                    {
                        ulong cycles = (ulong)usConst.Value * (cfg.Frequency / 1000000);
                        ulong loops = cycles / 6;
                        if (loops > 0) continue; 
                    }
                    AddRef(c.FunctionName);
                }
                // VirtualCall: the DefiningClass implementation must survive DCE.
                if (instr is VirtualCall vc2)
                    AddRef(vc2.DefiningClass + "_" + vc2.MethodName);
                var valsToCheck = instr switch
                {
                    Binary b => new[] { b.Src1, b.Src2, b.Dst },
                    Copy cp => new[] { cp.Src, cp.Dst },
                    Return r => r.Value != null ? new[] { r.Value } : Array.Empty<Val>(),
                    Call cl => [.. cl.Args, cl.Dst],
                    ArrayStore ast => new[] { ast.Src },
                    _ => Array.Empty<Val>()
                };
                foreach (var v in valsToCheck)
                {
                    if (v is FunctionRef fr) AddRef(fr.FunctionName);
                }
            }
        }
        // ------------------------------------

        foreach (var func in program.Functions.Where(func => !func.IsInterrupt)
                     .Where(func => referencedFuncs.Contains(func.Name))
                     .Where(func => !func.IsInline || func.Name == "main"))
        {
            CompileFunction(func);
        }

        // In release mode (no debug comments), shorten long generated symbol names
        // so the .asm file is easier to read. Full names are preserved in debug/linemap mode.
        if (!cfg.EmitDebugComments)
            ApplySymbolShortening(program);

        var optimized = AvrPeephole.Optimize(_assembly);
        foreach (var line in optimized)
            output.WriteLine(line.ToString());

        EmitFlashArrayPool(output);
        if (program.NeedsGc) EmitGcRuntime(output);
        // Emit the exception runtime when the T-flag model calls __pymcu_unhandled_exn
        // for an unmatched catch.
        bool needsExnRuntime = program.Functions.Any(f =>
                f.Body.OfType<Call>().Any(c => c.FunctionName == "__pymcu_unhandled_exn"));
        if (needsExnRuntime) EmitExnRuntime(output, _usedExnCodes, cfg.Chip);
        WriteSymbolsIfRequested(optimized, program);
        WriteLineMapIfRequested(optimized);
        WriteVarMapIfRequested(program);
    }

    public override void EmitContextSave()
    {
        EmitComment("ISR prologue -- save context");
        // R0 is clobbered by every MUL; R1 is the zero register assumed by SBC/ADC after MUL.
        // avr-gcc saves both in every ISR to prevent corruption of the interrupted context.
        // Save the full caller-clobbered range the ISR body — or any function it calls — may
        // use as scratch. The earlier fixed set omitted R20/R21 (the 16-bit MUL accumulator),
        // R22/R23 (32-bit math and the 3rd/4th argument pair), and R30/R31 (the Z pointer for
        // array/flash access). An ISR using any of those silently corrupted the interrupted
        // code's value held there (e.g. a uint16 multiply in the ISR clobbered main's R20:R21).
        // R28/R29 (Y, the frame pointer) is used only as a base, never reassigned, so it is
        // preserved without saving; R2-R15 are the globally-unique named-var pool (an ISR only
        // touches its OWN names there, never the interrupted function's).
        foreach (var r in new[] { "R0", "R1", "R16", "R17", "R18", "R19", "R20", "R21",
                                  "R22", "R23", "R24", "R25", "R26", "R27", "R30", "R31" })
            Emit("PUSH", r);
        Emit("IN", "R16", "0x3F");
        Emit("PUSH", "R16");
        // Ensure R1 == 0 inside the ISR body (MUL may have left it non-zero in main).
        Emit("CLR", "R1");
    }

    public override void EmitContextRestore()
    {
        EmitComment("ISR epilogue -- restore context");
        Emit("POP", "R16");
        Emit("OUT", "0x3F", "R16");
        foreach (var r in new[] { "R31", "R30", "R27", "R26", "R25", "R24", "R23", "R22",
                                  "R21", "R20", "R19", "R18", "R17", "R16", "R1", "R0" })
            Emit("POP", r);
    }

    public override void EmitInterruptReturn() => Emit("RETI");

    // Parse "R12" -> 12; pointer tokens "X"/"Y"/"Z" (and their +/- forms) -> the pair's low reg
    // (26/28/30); else -1. Used by the ISR-save trimmer to learn which registers a body touches.
    private static int ParseRegToken(string s)
    {
        if (string.IsNullOrEmpty(s)) return -1;
        char c0 = s[0];
        if (c0 == 'R' && int.TryParse(s.AsSpan(1), out int n)) return n;
        if (s.Contains('X')) return 26;
        if (s.Contains('Z')) return 30;
        if (s.Contains('Y')) return 28;
        return -1;
    }

    private static void AddTouched(string op, HashSet<int> set)
    {
        int r = ParseRegToken(op);
        if (r < 0) return;
        set.Add(r);
        // Pointer tokens and (handled at call site) word ops occupy a register pair.
        if (op.Length > 0 && (op[0] == 'X' || op[0] == 'Y' || op[0] == 'Z')) set.Add(r + 1);
    }

    // Shrinks the conservative ISR context save (R0,R1,R16-R27,R30,R31,SREG) to the registers
    // the handler body actually uses. Purely subtractive: it removes a PUSH and the matching
    // POP(s) only for a register the body provably never touches, so it can never drop a
    // needed save. R1 and R16 are always kept (R16 backs the SREG save, R1 is the zero
    // register the prologue clears); if the body makes ANY call, the full set is kept (the
    // callee may clobber anything caller-saved).
    private void TrimIsrContextSave(int start, int end)
    {
        var used = new HashSet<int>();
        bool hasCall = false, hasMul = false;
        for (int i = start; i < end; i++)
        {
            var ln = _assembly[i];
            if (ln.Type != AvrAsmLine.LineType.Instruction) continue;
            string m = ln.Mnemonic;
            if (m is "PUSH" or "POP") continue;                                   // context-save itself
            if ((m == "IN" || m == "OUT") && (ln.Op1 == "0x3F" || ln.Op2 == "0x3F")) continue; // SREG save
            if (m is "CALL" or "RCALL" or "ICALL" or "EICALL") hasCall = true;
            if (m.StartsWith("MUL") || m.StartsWith("FMUL")) hasMul = true;
            AddTouched(ln.Op1, used);
            AddTouched(ln.Op2, used);
            // Word ops touch the high half of their register pair implicitly.
            if (m is "MOVW" or "ADIW" or "SBIW")
            {
                int a = ParseRegToken(ln.Op1); if (a >= 0) used.Add(a + 1);
                if (m == "MOVW") { int b = ParseRegToken(ln.Op2); if (b >= 0) used.Add(b + 1); }
            }
        }
        if (hasMul) { used.Add(0); used.Add(1); }
        if (hasCall) return;   // a callee may clobber any caller-saved register — keep the full save

        // Trimmable = the caller-saved registers the save covers, minus the always-kept R1/R16.
        var drop = new HashSet<int>();
        foreach (var r in new[] { 0, 17, 18, 19, 20, 21, 22, 23, 24, 25, 26, 27, 30, 31 })
            if (!used.Contains(r)) drop.Add(r);
        if (drop.Count == 0) return;

        for (int i = start; i < end; i++)
        {
            var ln = _assembly[i];
            if (ln.Type == AvrAsmLine.LineType.Instruction && ln.Mnemonic is "PUSH" or "POP"
                && ParseRegToken(ln.Op1) is int rr && drop.Contains(rr))
                _assembly[i] = AvrAsmLine.MakeEmpty();
        }
    }

    private void CompileFunction(Function func)
    {
        _currentFunction = func;
        _tmpRegLayout = AvrLinearScan.Allocate(func);
        foreach (var (name, _) in _tmpRegLayout)
            _allTmpRegNames.Add(name);

        DetectDivModFusions(func);
        DetectBytePackFusions(func);

        int funcAsmStart = _assembly.Count;
        EmitLabel(func.Name);

        if (func.IsInterrupt && !func.IsNaked) EmitContextSave();

        if (func.Name == "main")
        {
            Emit("CLR", "R1");
            Emit("LDI", "R16", "hi8(0x08FF)");
            Emit("OUT", "0x3E", "R16");
            Emit("LDI", "R16", "lo8(0x08FF)");
            Emit("OUT", "0x3D", "R16");
            Emit("LDI", "R28", "lo8(_stack_base)");
            Emit("LDI", "R29", "hi8(_stack_base)");
            if (_bssSize > 0)
            {
                var bssLoop = MakeLabel("L_BSS_LOOP");
                var bssEnd  = MakeLabel("L_BSS_END");
                Emit("LDI", "R26", "lo8(_stack_base)");
                Emit("LDI", "R27", "hi8(_stack_base)");
                Emit("LDI", "R30", "lo8(_bss_end)");
                Emit("LDI", "R31", "hi8(_bss_end)");
                Emit("CP",  "R26", "R30");
                Emit("CPC", "R27", "R31");
                Emit("BREQ", bssEnd);
                EmitLabel(bssLoop);
                Emit("ST", "X+", "R1");
                Emit("CP",  "R26", "R30");
                Emit("CPC", "R27", "R31");
                Emit("BRNE", bssLoop);
                EmitLabel(bssEnd);
            }
            if (_needsGc) Emit("CALL", "gc_init");
        }

        if (!func.IsInterrupt && func.Name != "main" && func.Params.Count > 0)
        {
            var paramSizes = func.Params
                .Select(p => _varSizes.TryGetValue(p, out var psz0) ? psz0 : 1).ToList();
            var argRegs = ArgBaseRegs(paramSizes);
            for (var k = 0; k < func.Params.Count && k < 4; k++)
            {
                var pname = func.Params[k];
                bool p16 = _varSizes.TryGetValue(pname, out int psz) && psz == 2;
                bool p32 = _varSizes.TryGetValue(pname, out int psz32) && psz32 == 4;
                bool pFloat = p32 && _varIsFloat.Contains(pname);
                // Float ABI: first float arg is in R22(byte0):R23(byte1):R24(byte2):R25(byte3).
                // uint32 ABI: first arg is in R24(byte0):R25(byte1):R22(byte2):R23(byte3).
                // For floats, use R22 as base; for uint32, use R24 as base.
                string aR = pFloat && k == 0 ? "R22" : argRegs[k];
                if (_regLayout.TryGetValue(pname, out var r))
                {
                    if (aR != r) Emit("MOV", r, aR);
                    if (p16 || p32) Emit("MOV", GetHighReg(r), GetHighReg(aR));
                    if (p32)
                    {
                        // For float k==0: bytes 2 and 3 are in R24 and R25.
                        // For uint32 k==0: bytes 2 and 3 are in R22 and R23.
                        string aR2 = pFloat && k == 0 ? "R24" : (k == 0 ? "R22" : $"R{int.Parse(aR[1..]) + 2}");
                        string aR3 = pFloat && k == 0 ? "R25" : (k == 0 ? "R23" : $"R{int.Parse(aR[1..]) + 3}");
                        Emit("MOV", $"R{int.Parse(r[1..]) + 2}", aR2);
                        Emit("MOV", $"R{int.Parse(r[1..]) + 3}", aR3);
                    }
                }
                else if (_stackLayout.TryGetValue(pname, out int off))
                {
                    // Skip if the parameter is never read as an IR Variable in this function body.
                    // Covers pure-asm functions whose bodies use calling-convention registers directly.
                    if (!IsVariableReadInBody(pname, func.Body))
                        continue;
                    int pSize = p32 ? 4 : (p16 ? 2 : 1);
                    bool nearY = off + (pSize - 1) < 64;
                    if (nearY)
                    {
                        Emit("STD", $"Y+{off}", aR);
                        if (p16 || p32) Emit("STD", $"Y+{off + 1}", GetHighReg(aR));
                        if (p32)
                        {
                            string aR2 = pFloat && k == 0 ? "R24" : (k == 0 ? "R22" : $"R{int.Parse(aR[1..]) + 2}");
                            string aR3 = pFloat && k == 0 ? "R25" : (k == 0 ? "R23" : $"R{int.Parse(aR[1..]) + 3}");
                            Emit("STD", $"Y+{off + 2}", aR2);
                            Emit("STD", $"Y+{off + 3}", aR3);
                        }
                    }
                    else
                    {
                        var abs = 0x0100 + off;
                        Emit("STS", $"0x{abs:X4}", aR);
                        if (p16 || p32) Emit("STS", $"0x{abs + 1:X4}", GetHighReg(aR));
                        if (p32)
                        {
                            string aR2 = pFloat && k == 0 ? "R24" : (k == 0 ? "R22" : $"R{int.Parse(aR[1..]) + 2}");
                            string aR3 = pFloat && k == 0 ? "R25" : (k == 0 ? "R23" : $"R{int.Parse(aR[1..]) + 3}");
                            Emit("STS", $"0x{abs + 2:X4}", aR2);
                            Emit("STS", $"0x{abs + 3:X4}", aR3);
                        }
                    }
                }
            }
        }

        // --- Outlining pre-scan ---
        // Find all InlineExpansionMarker groups in the function body.
        // Any group with 1+ occurrences gets outlined: first copy becomes a subroutine,
        // all occurrences (including the first) are replaced with RCALL.
        var inlineGroups = new Dictionary<string, List<(int start, int end)>>();
        {
            int depth = 0;
            int scanStart = -1;
            string? scanFunc = null;
            for (int k = 0; k < func.Body.Count; k++)
            {
                if (func.Body[k] is InlineExpansionMarker km)
                {
                    if (!km.IsEnd)
                    {
                        if (depth == 0) { scanStart = k; scanFunc = km.FuncName; }
                        depth++;
                    }
                    else
                    {
                        depth--;
                        if (depth == 0 && scanFunc != null)
                        {
                            if (!inlineGroups.ContainsKey(scanFunc))
                                inlineGroups[scanFunc] = new();
                            inlineGroups[scanFunc].Add((scanStart, k));
                            scanFunc = null;
                        }
                    }
                }
            }
        }

        // A region whose result is produced DIRECTLY by a trailing Call (a "passthrough", e.g.
        // `def run_it(self): return self.hook()`) must NOT be outlined: the callee leaves its
        // result in the return registers (R24:R25), the region's result temp coalesces with them,
        // and the outliner emits no move -- but the outlined call site reads the result from the
        // temp's own register (R16:R17), which the subroutine never writes => garbage. Keeping such
        // a region inline lets the call result be consumed in the caller's own allocation context.
        bool RegionIsCallPassthrough(int start, int end)
        {
            for (int k = end - 1; k > start; k--)   // last real instruction before the end marker
            {
                var si = func.Body[k];
                if (si is DebugLine or InlineExpansionMarker) continue;
                return si is Call { Dst: not NoneVal };
            }
            return false;
        }

        // Map funcName → subroutine label; collect subroutine body ranges.
        var outlinedLabels = new Dictionary<string, string>();
        var pendingSubroutines = new List<(string label, int start, int end)>();
        foreach (var (fname, ranges) in inlineGroups)
        {
            if (ranges.Any(r => RegionIsCallPassthrough(r.start, r.end))) continue;  // keep inline
            var label = MakeLabel("_pymcu_outline");
            outlinedLabels[fname] = label;
            // Body range: [ranges[0].start + 1, ranges[0].end) (exclusive of markers)
            pendingSubroutines.Add((label, ranges[0].start + 1, ranges[0].end));
        }

        // --- Main compilation loop with outlining ---
        // A marked region is either OUTLINED (replaced by an RCALL here, body emitted once as a
        // subroutine) or kept INLINE (body emitted here). The skip stack tracks, per open marker,
        // whether its body is being skipped (because it was outlined) or emitted inline. A region
        // NOT in outlinedLabels must be emitted inline -- the old code unconditionally skipped
        // every marked region, silently DROPPING any region we chose not to outline.
        bool emittedEpilogue = false;
        var skipStack = new Stack<bool>();
        bool Skipping() => skipStack.Count > 0 && skipStack.Peek();
        foreach (var instr in func.Body)
        {
            if (func.IsInterrupt && !func.IsNaked && instr is Return)
            {
                if (!Skipping())
                {
                    EmitContextRestore();
                    Emit("RETI");
                    emittedEpilogue = true;
                }
                continue;
            }

            if (instr is InlineExpansionMarker iem)
            {
                if (!iem.IsEnd)
                {
                    if (!Skipping() && outlinedLabels.TryGetValue(iem.FuncName, out var rcallLabel))
                    {
                        Emit("RCALL", rcallLabel);
                        skipStack.Push(true);   // outlined: skip the inline body
                    }
                    else
                    {
                        skipStack.Push(Skipping());  // inline (false) or stay inside a skipped region
                    }
                }
                else
                {
                    if (skipStack.Count > 0) skipStack.Pop();
                }
                continue;
            }

            if (Skipping()) continue;

            CompileInstruction(instr);
        }

        if (func.IsInterrupt && !func.IsNaked && !emittedEpilogue)
        {
            EmitContextRestore();
            Emit("RETI");
        }

        // Trim the (conservative) ISR context save down to the registers this handler's body
        // actually clobbers — recovers the size of trivial handlers without losing the
        // correctness of the full save (it only ever REMOVES a push/pop of an unused register).
        if (func.IsInterrupt && !func.IsNaked)
            TrimIsrContextSave(funcAsmStart, _assembly.Count);

        // --- Emit pending subroutines after the function body ---
        foreach (var (label, start, end) in pendingSubroutines)
        {
            EmitLabel(label);
            var subSkip = new Stack<bool>();
            bool SubSkipping() => subSkip.Count > 0 && subSkip.Peek();
            for (int i = start; i < end; i++)
            {
                var si = func.Body[i];
                if (si is InlineExpansionMarker sim)
                {
                    if (!sim.IsEnd)
                    {
                        if (!SubSkipping() && outlinedLabels.TryGetValue(sim.FuncName, out var sl))
                        {
                            Emit("RCALL", sl);
                            subSkip.Push(true);
                        }
                        else subSkip.Push(SubSkipping());
                    }
                    else if (subSkip.Count > 0) subSkip.Pop();
                    continue;
                }
                if (SubSkipping()) continue;
                CompileInstruction(si);
            }
            Emit("RET");
        }
    }

    private void CompileInstruction(Instruction instr)
    {
        switch (instr)
        {
            case Return r: CompileReturn(r); break;
            case Jump j: Emit("RJMP", j.Target); break;
            case JumpIfZero jz: CompileJumpIfZero(jz); break;
            case JumpIfNotZero jnz: CompileJumpIfNotZero(jnz); break;
            case Label l: EmitLabel(l.Name); break;
            case DebugLine d:
                if (cfg.EmitDebugComments)
                    EmitComment(string.IsNullOrEmpty(d.SourceFile)
                        ? $"Line {d.Line}: {d.Text}"
                        : $"{d.SourceFile}:{d.Line}: {d.Text}");
                if (!string.IsNullOrEmpty(d.SourceFile) && !d.IsInline)
                    _assembly.Add(AvrAsmLine.MakeDebugMarker(d.SourceFile, d.Line));
                break;
            case JumpIfEqual je: CompileCompareJump(je.Src1, je.Src2, "BREQ", je.Target); break;
            case JumpIfNotEqual jne: CompileCompareJump(jne.Src1, jne.Src2, "BRNE", jne.Target); break;
            case JumpIfLessThan jlt: CompileCompareJump(jlt.Src1, jlt.Src2, IsSignedComparison(jlt.Src1, jlt.Src2) ? "BRLT" : "BRLO", jlt.Target); break;
            case JumpIfLessOrEqual jle: CompileLessOrEqual(jle); break;
            case JumpIfGreaterThan jgt: CompileGreaterThan(jgt); break;
            case JumpIfGreaterOrEqual jge: CompileCompareJump(jge.Src1, jge.Src2, IsSignedComparison(jge.Src1, jge.Src2) ? "BRGE" : "BRSH", jge.Target); break;
            case Call c: CompileCall(c); break;
            case IndirectCall ic: CompileIndirectCall(ic); break;
            case VirtualCall vc: CompileVirtualCall(vc); break;
            case Copy cp: CompileCopy(cp); break;
            case Bitcast bc: CompileBitcast(bc); break;
            case LoadIndirect li: CompileLoadIndirect(li); break;
            case StoreIndirect si: CompileStoreIndirect(si); break;
            case Unary u: CompileUnary(u); break;
            case Binary b: CompileBinary(b); break;
            case BitSet bs: CompileBitSet(bs); break;
            case BitClear bc: CompileBitClear(bc); break;
            case BitCheck bck: CompileBitCheck(bck); break;
            case BitWrite bw: CompileBitWrite(bw); break;
            case JumpIfBitSet jbs: CompileJumpIfBitSet(jbs); break;
            case JumpIfBitClear jbc: CompileJumpIfBitClear(jbc); break;
            case AugAssign aa: CompileAugAssign(aa); break;
            case InlineAsm asm2:
                if (asm2.Operands == null || asm2.Operands.Count == 0)
                {
                    _assembly.Add(AvrAsmLine.MakeRaw(InterpolateAsmSymbols(asm2.Code)));
                }
                else
                {
                    CompileInlineAsmWithConstraints(asm2);
                }
                break;
            case ArrayLoad al: CompileArrayLoad(al); break;
            case ArrayLoadFlash alf: CompileArrayLoadFlash(alf); break;
            case FlashLoadPtr flp: CompileFlashLoadPtr(flp); break;
            case FlashData fd: _flashArrayPool[fd.Name] = fd.Bytes; break;
            case ArrayStore ast: CompileArrayStore(ast); break;
            case BytearrayLoad bl: CompileBytearrayLoad(bl); break;
            case BytearrayStore bs2: CompileBytearrayStore(bs2); break;
            case GcAlloc ga:  CompileGcAlloc(ga);  break;
            case GcRoot gr:   CompileGcRoot(gr);   break;
            case GcUnroot gu: CompileGcUnroot(gu); break;
            case SignalError se: CompileSignalError(se); break;
            case SignalSuccess: CompileSignalSuccess(); break;
            case BranchOnError boe: CompileBranchOnError(boe); break;
        }
    }

    private void CompileReturn(Return r)
    {
        if (r.Value is not NoneVal)
        {
            var returnType = _currentFunction?.ReturnType ?? GetValType(r.Value);
            if (returnType == DataType.FLOAT)
                LoadFloatIntoRegs(r.Value);
            else
                LoadIntoReg(r.Value, "R24", returnType);
        }

        // Inject CLT before RET in every CanFail function's happy path.
        // This guarantees T == 0 when the caller reads the flag, regardless of
        // which other CanFail callees the function may have called along the way.
        // ISRs are excluded: (a) they cannot be CanFail (enforced by CanFailAnalyzer)
        // and (b) SREG is restored from the saved context by EmitContextRestore().
        if ((_currentFunction?.CanFail ?? false) && !(_currentFunction?.IsInterrupt ?? false))
            Emit("CLT");

        if (!(_currentFunction?.IsNaked ?? false))
            Emit("RET");
    }

    private void CompileJumpIfZero(JumpIfZero jz)
    {
        var type = GetValType(jz.Condition);
        LoadIntoReg(jz.Condition, "R24", type);

        if (type.SizeOf() == 4)
        {
            Emit("OR", "R24", "R25");
            Emit("OR", "R24", "R22");
            Emit("OR", "R24", "R23");
            EmitBranch("BREQ", jz.Target);
        }
        else if (type.SizeOf() == 2)
        {
            Emit("OR", "R24", "R25"); // Combine low and high, this sets the Z flag
            EmitBranch("BREQ", jz.Target);
        }
        else
        {
            Emit("TST", "R24"); // Only test if it's an 8-bit value
            EmitBranch("BREQ", jz.Target);
        }
    }

    private void CompileJumpIfNotZero(JumpIfNotZero jnz)
    {
        var type = GetValType(jnz.Condition);
        LoadIntoReg(jnz.Condition, "R24", type);
        // OR R24, R25 already sets the Z flag for 16-bit values; no separate TST needed.
        if (type.SizeOf() == 4) { Emit("OR", "R24", "R25"); Emit("OR", "R24", "R22"); Emit("OR", "R24", "R23"); }
        else if (type.SizeOf() == 2) Emit("OR", "R24", "R25");
        else Emit("TST", "R24");
        EmitBranch("BRNE", jnz.Target);
    }

    // 16-bit compare against a compile-time constant.
    // When the constant fits in one byte (hi==0), use CPI+CPC R1 (saves 2 LDI words).
    // R1 is the AVR zero register and is always 0 in PyMCU-generated code.
    private void Emit16BitCompareConstant(int val)
    {
        if ((val & 0xFF00) == 0)
        {
            Emit("CPI", "R24", $"{val & 0xFF}");
            Emit("CPC", "R25", "R1");
        }
        else
        {
            Emit("LDI", "R18", $"{val & 0xFF}");
            Emit("LDI", "R19", $"{(val >> 8) & 0xFF}");
            Emit("CP",  "R24", "R18");
            Emit("CPC", "R25", "R19");
        }
    }

    private void EmitCompare(Val src1, Val src2, DataType type)
    {
        LoadIntoReg(src1, "R24", type);
        if (src2 is Constant c)
        {
            var val = c.Value;
            if (type.SizeOf() == 4)
            {
                Emit("LDI", "R18", $"{val & 0xFF}");
                Emit("LDI", "R19", $"{(val >> 8) & 0xFF}");
                Emit("LDI", "R20", $"{(val >> 16) & 0xFF}");
                Emit("LDI", "R21", $"{(val >> 24) & 0xFF}");
                Emit("CP",  "R24", "R18");
                Emit("CPC", "R25", "R19");
                Emit("CPC", "R22", "R20");
                Emit("CPC", "R23", "R21");
            }
            else if (type.SizeOf() == 2)
                Emit16BitCompareConstant(val);
            else
                Emit("CPI", "R24", $"{val & 0xFF}");
        }
        else
        {
            LoadIntoReg(src2, "R18", type);
            Emit("CP", "R24", "R18");
            if (type.SizeOf() == 2) Emit("CPC", "R25", "R19");
            if (type.SizeOf() == 4) { Emit("CPC", "R25", "R19"); Emit("CPC", "R22", "R20"); Emit("CPC", "R23", "R21"); }
        }
    }

    private void CompileCompareJump(Val src1, Val src2, string branch, string target)
    {
        var type = GetValType(src1);
        if (type == DataType.FLOAT)
        {
            // __fp_cmp(arg0=R22:R25, arg1=R18:R21): returns 0xFF if <, 0x00 if =, 0x01 if >
            LoadFloatIntoRegs(src1);
            Emit("PUSH", "R25");
            Emit("PUSH", "R24");
            Emit("PUSH", "R23");
            Emit("PUSH", "R22");
            LoadFloatIntoRegs(src2);
            Emit("MOV", "R18", "R22");
            Emit("MOV", "R19", "R23");
            Emit("MOV", "R20", "R24");
            Emit("MOV", "R21", "R25");
            Emit("POP", "R22");
            Emit("POP", "R23");
            Emit("POP", "R24");
            Emit("POP", "R25");
            Emit("CALL", "__cmpsf2");
            // Map branch to __cmpsf2 result: 0xFF=lt, 0x00=eq, 0x01=gt
            switch (branch)
            {
                case "BRGE": case "BRSH":
                    Emit("CPI", "R24", "0xFF"); EmitBranch("BRNE", target); break;
                case "BRLT": case "BRLO":
                    Emit("CPI", "R24", "0xFF"); EmitBranch("BREQ", target); break;
                case "BREQ":
                    Emit("CPI", "R24", "0x00"); EmitBranch("BREQ", target); break;
                case "BRNE":
                    Emit("CPI", "R24", "0x00"); EmitBranch("BRNE", target); break;
                default:
                    Emit("CPI", "R24", "0xFF"); EmitBranch("BRNE", target); break;
            }
            return;
        }
        EmitCompare(src1, src2, type);
        EmitBranch(branch, target);
    }

    private void CompileLessOrEqual(JumpIfLessOrEqual jle)
    {
        var type = GetValType(jle.Src1);
        bool signed = IsSignedComparison(jle.Src1, jle.Src2);
        string brLo = signed ? "BRLT" : "BRLO";

        // val <= const  ≡  val < (const+1): saves one EmitBranch (2 instructions).
        if (jle.Src2 is Constant cLE)
        {
            int maxVal = type.SizeOf() == 2 ? (signed ? 0x7FFF : 0xFFFF)
                       : type.SizeOf() == 4 ? int.MaxValue
                       : (signed ? 0x7F : 0xFF);
            if (cLE.Value < maxVal)
            {
                int cmpVal = cLE.Value + 1;
                LoadIntoReg(jle.Src1, "R24", type);
                if (type.SizeOf() == 4)
                {
                    Emit("LDI", "R18", $"{cmpVal & 0xFF}");
                    Emit("LDI", "R19", $"{(cmpVal >> 8) & 0xFF}");
                    Emit("LDI", "R20", $"{(cmpVal >> 16) & 0xFF}");
                    Emit("LDI", "R21", $"{(cmpVal >> 24) & 0xFF}");
                    Emit("CP",  "R24", "R18");
                    Emit("CPC", "R25", "R19");
                    Emit("CPC", "R22", "R20");
                    Emit("CPC", "R23", "R21");
                }
                else if (type.SizeOf() == 2)
                    Emit16BitCompareConstant(cmpVal);
                else
                    Emit("CPI", "R24", $"{cmpVal & 0xFF}");
                EmitBranch(brLo, jle.Target);
                return;
            }
        }

        EmitCompare(jle.Src1, jle.Src2, type);
        EmitBranch(brLo, jle.Target);
        EmitBranch("BREQ", jle.Target);
    }

    private void CompileGreaterThan(JumpIfGreaterThan jgt)
    {
        var type = GetValType(jgt.Src1);
        bool signed = IsSignedComparison(jgt.Src1, jgt.Src2);
        LoadIntoReg(jgt.Src1, "R24", type);

        if (jgt.Src2 is Constant c)
        {
            int val = c.Value;
            if (type.SizeOf() == 4)
            {
                if (val < int.MaxValue)
                {
                    long cmpVal = (long)val + 1;
                    Emit("LDI", "R18", $"{cmpVal & 0xFF}");
                    Emit("LDI", "R19", $"{(cmpVal >> 8) & 0xFF}");
                    Emit("LDI", "R20", $"{(cmpVal >> 16) & 0xFF}");
                    Emit("LDI", "R21", $"{(cmpVal >> 24) & 0xFF}");
                    Emit("CP",  "R24", "R18");
                    Emit("CPC", "R25", "R19");
                    Emit("CPC", "R22", "R20");
                    Emit("CPC", "R23", "R21");
                    EmitBranch(signed ? "BRGE" : "BRSH", jgt.Target);
                }
                return;
            }
            int maxVal = type.SizeOf() == 2 ? (signed ? 0x7FFF : 0xFFFF) : (signed ? 0x7F : 0xFF);
            if (val < maxVal)
            {
                int cmpVal = val + 1;
                if (type.SizeOf() == 2)
                    Emit16BitCompareConstant(cmpVal);
                else
                    Emit("CPI", "R24", $"{cmpVal & 0xFF}");

                EmitBranch(signed ? "BRGE" : "BRSH", jgt.Target);
            }

            return; // a > max is always false
        }

        LoadIntoReg(jgt.Src2, "R18", type);
        Emit("CP", "R24", "R18");
        if (type.SizeOf() == 2) Emit("CPC", "R25", "R19");
        if (type.SizeOf() == 4) { Emit("CPC", "R25", "R19"); Emit("CPC", "R22", "R20"); Emit("CPC", "R23", "R21"); }
        var skip = MakeLabel("L_BRHI_SKIP");
        Emit("BREQ", skip);
        EmitBranch(signed ? "BRGE" : "BRSH", jgt.Target);
        EmitLabel(skip);
    }

    private void CompileCall(Call call)
    {
        if ((call.FunctionName == "_delay_ms_avr" || call.FunctionName.EndsWith("__delay_ms_avr")) && call.Args.Count == 1 && call.Args[0] is Constant msConst)
        {
            ulong cycles = (ulong)msConst.Value * (cfg.Frequency / 1000);
            ulong loops = cycles / 6;
            if (loops == 0) return;

            string label = $"_dly_L{_loopCounter++}";

            Emit($"LDI", "R18", $"{(loops & 0xFF)}");
            Emit($"LDI", "R19", $"{((loops >> 8) & 0xFF)}");
            Emit($"LDI", "R20", $"{((loops >> 16) & 0xFF)}");
            Emit($"LDI", "R21", $"{((loops >> 24) & 0xFF)}");
            EmitLabel(label);
            Emit($"SUBI", "R18", "1");
            Emit($"SBCI", "R19", "0");
            Emit($"SBCI", "R20", "0");
            Emit($"SBCI", "R21", "0");
            Emit("BRNE", label);
            return;
        }
        // delay_us(const): same calibrated 6-cycle busy loop as delay_ms, sized in
        // microseconds. Emitted inline so a constant micro-delay is a tight loop instead
        // of a CALL to (or per-site inline of) the 12-NOP _delay_us_avr body. The matching
        // DCE walk skips AddRef for this same const case, so the subroutine is only
        // compiled when a runtime (non-constant) delay_us call needs it.
        if ((call.FunctionName == "_delay_us_avr" || call.FunctionName.EndsWith("__delay_us_avr")) && call.Args.Count == 1 && call.Args[0] is Constant usConst)
        {
            ulong cycles = (ulong)usConst.Value * (cfg.Frequency / 1000000);
            ulong loops = cycles / 6;
            if (loops == 0) return;

            string label = $"_dly_L{_loopCounter++}";

            Emit($"LDI", "R18", $"{(loops & 0xFF)}");
            Emit($"LDI", "R19", $"{((loops >> 8) & 0xFF)}");
            Emit($"LDI", "R20", $"{((loops >> 16) & 0xFF)}");
            Emit($"LDI", "R21", $"{((loops >> 24) & 0xFF)}");
            EmitLabel(label);
            Emit($"SUBI", "R18", "1");
            Emit($"SBCI", "R19", "0");
            Emit($"SBCI", "R20", "0");
            Emit($"SBCI", "R21", "0");
            Emit("BRNE", label);
            return;
        }
        // Float arguments use R22:R25 (arg0) and R18:R21 (arg1) per float convention.
        // Integer arguments are assigned by size (ArgBaseRegs) so a 32-bit arg gets a 4-register
        // block and does not collide with the previous argument.
        _functionParamSizes.TryGetValue(call.FunctionName, out var ps);
        var argSizes = new List<int>(call.Args.Count);
        for (var k = 0; k < call.Args.Count; k++)
            argSizes.Add(ps != null && k < ps.Count ? ps[k] : GetValType(call.Args[k]).SizeOf());
        var argRegs = ArgBaseRegs(argSizes);
        for (var k = 0; k < call.Args.Count && k < 4; k++)
        {
            var argType = GetValType(call.Args[k]);
            if (argType == DataType.FLOAT)
            {
                // Float arg0 → R22:R25; float arg1 → R18:R21
                if (k == 0)
                    LoadFloatIntoRegs(call.Args[k]);
                else if (k == 1)
                {
                    LoadFloatIntoRegs(call.Args[k]);
                    Emit("MOV", "R18", "R22");
                    Emit("MOV", "R19", "R23");
                    Emit("MOV", "R20", "R24");
                    Emit("MOV", "R21", "R25");
                }
                continue;
            }
            // Use the declared parameter size when available so that constants
            // (e.g. Constant(-1) for an int16 param) are loaded with the correct
            // width instead of the size inferred from the constant's magnitude.
            if (_functionParamSizes.TryGetValue(call.FunctionName, out var paramSizes) &&
                k < paramSizes.Count)
            {
                int paramSize = paramSizes[k];
                if (paramSize >= 2 && argType.SizeOf() < paramSize)
                    argType = paramSize == 4 ? DataType.UINT32 : DataType.UINT16;
            }
            LoadIntoReg(call.Args[k], argRegs[k], argType);
        }

        Emit("CALL", call.FunctionName);
        var dstType = GetValType(call.Dst);
        if (dstType == DataType.FLOAT)
            StoreFloatFromRegs(call.Dst);
        else
            StoreRegInto("R24", call.Dst, dstType);
    }

    // Indirect call through a function pointer (ICALL Z on AVR).
    // FuncAddr must be a FUNCREF-typed variable or a FunctionRef Val.
    // The function address is loaded into Z (R30:R31) and ICALL is emitted.
    private void CompileIndirectCall(IndirectCall call)
    {
        // Set up arguments into standard registers (same ABI as direct Call): size-based base
        // assignment so a 32-bit argument occupies a 4-register block without colliding.
        var argRegs = ArgBaseRegs(call.Args.Select(a => GetValType(a).SizeOf()).ToList());
        for (var k = 0; k < call.Args.Count && k < 4; k++)
        {
            var argType = GetValType(call.Args[k]);
            LoadIntoReg(call.Args[k], argRegs[k], argType);
        }

        // Load function address into Z register (R30:R31).
        LoadIntoReg(call.FuncAddr, "R30", DataType.UINT16);

        Emit("ICALL");

        var dstType = GetValType(call.Dst);
        StoreRegInto("R24", call.Dst, dstType);
    }

    // -------------------------------------------------------------------------
    // Virtual dispatch (flash vtable + ICALL Z)
    // -------------------------------------------------------------------------

    /// <summary>
    /// Emit code for a VirtualCall that survived the devirtualization pass.
    ///
    /// Object layout: self is a SRAM struct whose first 2 bytes (vptr lo/hi) point
    /// to the class's flash-resident vtable.
    ///
    ///   LDD  R30, Y+&lt;self_lo&gt;      ; vptr lo
    ///   LDD  R31, Y+&lt;self_lo&gt;+1   ; vptr hi (vptr is a 2-byte flash address)
    ///   SUBI R30, lo8(-&lt;slot*2&gt;)  ; advance by slot index × 2
    ///   SBCI R31, hi8(-&lt;slot*2&gt;)
    ///   LPM  R0,  Z+               ; target function address lo (byte)
    ///   LPM  R31, Z                ; target function address hi (byte)
    ///   MOV  R30, R0
    ///   ;; load args into R24, R22, R20, R18 per ABI
    ///   ICALL
    /// </summary>
    private void CompileVirtualCall(VirtualCall vc)
    {
        // Load self pointer into Z (vptr is the first 2 bytes of the object in SRAM).
        string selfName = vc.Self.Name.Replace('.', '_');
        if (_stackLayout.TryGetValue(selfName, out int selfOffset))
        {
            Emit("LDD", "R30", $"Y+{selfOffset}");
            Emit("LDD", "R31", $"Y+{selfOffset + 1}");
        }
        else
        {
            // Register-allocated: load address via MOVW / STS fallback.
            Emit("LDI", "R30", $"lo8({selfName})");
            Emit("LDI", "R31", $"hi8({selfName})");
        }

        // Read vptr (first 2 SRAM bytes of self) into Z.
        Emit("LD",  "R30", "Z+");
        Emit("LD",  "R31", "Z");
        Emit("SBIW", "R30", "2");   // undo the +2 from the Z+ above

        // Advance Z by slotIndex * 2.
        int slotOffset = vc.SlotIndex * 2;
        if (slotOffset > 0)
        {
            Emit("SUBI", "R30", $"lo8(-{slotOffset})");
            Emit("SBCI", "R31", $"hi8(-{slotOffset})");
        }

        // LPM: read 16-bit function address from flash (Z points to vtable slot).
        Emit("LPM", "R0",  "Z+");
        Emit("LPM", "R31", "Z");
        Emit("MOV", "R30", "R0");

        // Load self as arg 0, then the remaining arguments (size-based base assignment).
        var allArgs = new List<Val> { vc.Self };
        allArgs.AddRange(vc.Args);
        var argRegs = ArgBaseRegs(allArgs.Select(a => GetValType(a).SizeOf()).ToList());
        for (int k = 0; k < allArgs.Count && k < argRegs.Count; k++)
        {
            var argType = GetValType(allArgs[k]);
            LoadIntoReg(allArgs[k], argRegs[k], argType);
        }

        Emit("ICALL");

        var dstType = GetValType(vc.Dst);
        if (dstType == DataType.FLOAT)
            StoreFloatFromRegs(vc.Dst);
        else
            StoreRegInto("R24", vc.Dst, dstType);
    }

    /// <summary>
    /// Emit flash-resident vtables for classes in <c>program.Vtables</c>.
    /// Each vtable is a sequence of .word entries in PROGMEM; each .word holds
    /// the byte address of the implementing function.
    ///
    /// Called only when VirtualCall nodes survived the devirtualization pass —
    /// emits nothing (zero flash overhead) for ordinary programs.
    /// </summary>
    private void EmitVtables(ProgramIR program)
    {
        if (program.Vtables == null || program.Vtables.Count == 0) return;

        EmitRaw(".section .rodata");
        foreach (var vt in program.Vtables)
        {
            EmitRaw("");
            EmitLabel($"{vt.ClassName}__vtable");
            foreach (var entry in vt.Entries)
                EmitRaw($"    .word {entry.DefiningClass}_{entry.MethodName}");
        }
        EmitRaw(".section .text");
        EmitRaw("");
    }

    private void CompileCopy(Copy cp)
    {
        var srcType = GetValType(cp.Src);
        var dstType = GetValType(cp.Dst);
        if (srcType == DataType.FLOAT && dstType != DataType.FLOAT)
        {
            // Float → integer: load float into R22:R25, __fixsfsi converts to int32 (R22=LSB).
            LoadFloatIntoRegs(cp.Src);
            Emit("CALL", "__fixsfsi");
            Emit("MOV", "R24", "R22");
            Emit("MOV", "R25", "R23");
            StoreRegInto("R24", cp.Dst, dstType);
            return;
        }
        if (srcType == DataType.FLOAT || dstType == DataType.FLOAT)
        {
            LoadFloatIntoRegs(cp.Src);
            StoreFloatFromRegs(cp.Dst);
            return;
        }
        // When src is a typeless constant, use the destination's declared type
        // to ensure e.g. `i: uint16 = 0` initialises both bytes.
        var loadType = cp.Src is Constant ? dstType : dstType;
        LoadIntoReg(cp.Src, "R24", loadType);
        StoreRegInto("R24", cp.Dst, dstType);
    }

    private void CompileLoadIndirect(LoadIndirect li)
    {
        LoadIntoReg(li.SrcPtr, "R26", DataType.UINT16);
        DataType dstType = GetValType(li.Dst);
        int dstSize = dstType.SizeOf();
        if (dstSize == 4)
        {
            Emit("LD", "R24", "X+");
            Emit("LD", "R25", "X+");
            Emit("LD", "R22", "X+");
            Emit("LD", "R23", "X");
        }
        else if (dstSize == 2)
        {
            Emit("LD", "R24", "X+");
            Emit("LD", "R25", "X");
        }
        else Emit("LD", "R24", "X");
        StoreRegInto("R24", li.Dst, dstType);
    }

    private void CompileStoreIndirect(StoreIndirect si)
    {
        LoadIntoReg(si.DstPtr, "R26", DataType.UINT16);
        DataType srcType = GetValType(si.Src);
        LoadIntoReg(si.Src, "R24", srcType);
        int srcSize = srcType.SizeOf();
        if (srcSize == 4)
        {
            Emit("ST", "X+", "R24");
            Emit("ST", "X+", "R25");
            Emit("ST", "X+", "R22");
            Emit("ST", "X",  "R23");
        }
        else if (srcSize == 2)
        {
            Emit("ST", "X+", "R24");
            Emit("ST", "X",  "R25");
        }
        else Emit("ST", "X", "R24");
    }

    private void CompileBitcast(Bitcast bc)
    {
        var srcType = GetValType(bc.Src);
        var dstType = GetValType(bc.Dst);
        bool srcIsFloat = srcType == DataType.FLOAT;
        bool dstIsFloat = dstType == DataType.FLOAT;

        if (srcIsFloat && !dstIsFloat)
        {
            // float → intN: float reg layout: R22=b0, R23=b1, R24=b2, R25=b3
            // uint32 reg layout:              R24=b0, R25=b1, R22=b2, R23=b3
            LoadFloatIntoRegs(bc.Src);
            Emit("PUSH", "R24");        // save b2
            Emit("PUSH", "R25");        // save b3
            Emit("MOV", "R24", "R22"); // R24 = b0
            Emit("MOV", "R25", "R23"); // R25 = b1
            Emit("POP", "R23");        // R23 = b3
            Emit("POP", "R22");        // R22 = b2
            StoreRegInto("R24", bc.Dst, dstType);
        }
        else if (!srcIsFloat && dstIsFloat)
        {
            // intN → float: always treat source as 32-bit (float is 4 bytes).
            // GetValType(Constant) only returns UINT8/UINT16; force UINT32 so all
            // 4 bytes of the constant are loaded into R24:R25:R22:R23.
            // uint32 reg layout: R24=b0, R25=b1, R22=b2, R23=b3
            // float reg layout:  R22=b0, R23=b1, R24=b2, R25=b3
            LoadIntoReg(bc.Src, "R24", DataType.UINT32);
            Emit("PUSH", "R22");        // save b2
            Emit("PUSH", "R23");        // save b3
            Emit("MOV", "R22", "R24"); // R22 = b0
            Emit("MOV", "R23", "R25"); // R23 = b1
            Emit("POP", "R25");        // R25 = b3
            Emit("POP", "R24");        // R24 = b2
            StoreFloatFromRegs(bc.Dst);
        }
        else
        {
            // No float involved: same byte layout, just type reinterpretation
            CompileCopy(new Copy(bc.Src, bc.Dst));
        }
    }

    private void CompileUnary(Unary u)
    {
        DataType type = GetValType(u.Dst);
        LoadIntoReg(u.Src, "R24", type);
        bool is16 = type.SizeOf() == 2;
        bool is32 = type.SizeOf() == 4;

        switch (u.Op)
        {
            case IrUnOp.Neg:
                // Two's-complement negation using the NEG/COM/SBCI carry-chain.
                // NEG R24 sets C = (R24_original != 0).
                // Each subsequent byte: COM Rn ; SBCI Rn, 255
                //   computes ~Rn + 1 - C, which is the correct borrow-propagated byte.
                // avr-gcc emits the identical sequence for all widths.
                Emit("NEG", "R24");
                if (is16 || is32)
                {
                    Emit("COM", "R25");
                    Emit("SBCI", "R25", "255");
                }
                if (is32)
                {
                    Emit("COM", "R22");
                    Emit("SBCI", "R22", "255");
                    Emit("COM", "R23");
                    Emit("SBCI", "R23", "255");
                }
                break;
            case IrUnOp.BitNot:
                Emit("COM", "R24");
                if (is16 || is32) Emit("COM", "R25");
                if (is32) { Emit("COM", "R22"); Emit("COM", "R23"); }
                break;
            case IrUnOp.Not:
                var lTrue = MakeLabel("L_NOT_TRUE");
                var lDone = MakeLabel("L_NOT_DONE");
                if (is16) Emit("OR", "R24", "R25");
                Emit("TST", "R24");
                EmitBranch("BREQ", lTrue);
                Emit("CLR", "R24");
                if (is16) Emit("CLR", "R25");
                Emit("RJMP", lDone);
                EmitLabel(lTrue);
                Emit("LDI", "R24", "1");
                EmitLabel(lDone);
                break;
        }

        StoreRegInto("R24", u.Dst, type);
    }

    // Destination-targeted in-place codegen for 8-bit Add/Sub/And/Or/Xor where the result
    // lives directly in dst's home register. This removes the codegen's "stage through R24,
    // store home" round-trip: for `x = x + y` with x in a home register it collapses to a
    // single `ADD Rx, Ry` (vs MOV/op/MOV). Returns false — emitting nothing — whenever the
    // AVR register classes or operand shapes don't permit the in-place form, so the caller
    // falls back to the existing staged path. Validation is done up front; emission only
    // happens once the whole op is known to be feasible.
    private bool TryCompileBinaryInPlace(Binary b)
    {
        DataType type = GetValType(b.Dst);
        if (type is not (DataType.UINT8 or DataType.INT8
                         or DataType.UINT16 or DataType.INT16)) return false;
        int size = type.SizeOf();   // 1 or 2
        if (b.Op is not (IrBinOp.Add or IrBinOp.Sub or IrBinOp.BitAnd
                         or IrBinOp.BitOr or IrBinOp.BitXor)) return false;

        // dst must be register-resident; an SRAM dst gains nothing from in-place codegen.
        string? rd = OperandHomeReg(b.Dst);
        if (rd is null) return false;
        bool rdUpper = int.Parse(rd[1..]) >= 16;   // R16-R31 accept LDI/SUBI/ANDI/ORI

        // src1 must reach rd via MOV/LDD (any register) — not LDI, which is illegal for the
        // low half. So only a same-width Variable/Temporary qualifies (constants/addresses out).
        if (b.Src1 is not (Variable or Temporary)) return false;
        if (GetValType(b.Src1).SizeOf() != size) return false;

        string? rs1 = OperandHomeReg(b.Src1);
        string? rs2 = OperandHomeReg(b.Src2);

        // Restrict to the augmented form (dst == src1, i.e. src1 already in rd). For
        // dst != src1 the staged path leaves the result in R24 where the next op can reuse
        // it, so an in-place `MOV rd,src1; OP rd,...` tends to merely move the extra MOV
        // rather than remove it. The augmented case is the unambiguous win: the staged path
        // is always MOV/op/MOV, the in-place form collapses it to a single op on rd.
        // (Measured 2026-06-14: relaxing this to dst != src1 regressed the example suite by
        // ~100 bytes — the store-back MOV reappears as reload MOVs in chained expressions.)
        if (rs1 != rd) return false;

        // src1 already lives in rd, so no load clobbers it; src2 in rd just means `x op x`.

        string mnem = b.Op switch
        {
            IrBinOp.Add    => "ADD",
            IrBinOp.Sub    => "SUB",
            IrBinOp.BitAnd => "AND",
            IrBinOp.BitOr  => "OR",
            IrBinOp.BitXor => "EOR",
            _              => "",
        };

        // --- Constant src2: must fit an immediate/inc-dec form valid for rd's class. ---
        if (b.Src2 is Constant c)
        {
            // 16-bit immediates need SUBI/SBCI on an R16-R31 pair; register-pair homes here
            // are always R4-R15 (the temporary pool is 8-bit), so fall back for 16-bit consts.
            if (size != 1) return false;
            if (b.Op is IrBinOp.BitXor) return false;     // no immediate EOR form
            int v = c.Value & 0xFF;
            string? emit = b.Op switch
            {
                IrBinOp.Add when v == 1   => "INC",
                IrBinOp.Add when v == 0xFF => "DEC",
                IrBinOp.Sub when v == 1   => "DEC",
                IrBinOp.Sub when v == 0xFF => "INC",
                _ => null,
            };
            if (emit is null && !rdUpper) return false;   // immediate form needs R16-R31

            LoadIntoReg(b.Src1, rd, type);
            if (emit is not null) Emit(emit, rd);
            else switch (b.Op)
            {
                case IrBinOp.Add:    Emit("SUBI", rd, $"{(byte)(-v)}"); break;
                case IrBinOp.Sub:    Emit("SUBI", rd, $"{v}"); break;
                case IrBinOp.BitAnd: Emit("ANDI", rd, $"{v}"); break;
                case IrBinOp.BitOr:  Emit("ORI",  rd, $"{v}"); break;
            }
            return true;
        }

        // --- Register/SRAM src2: byte-wise OP rd, <reg>. ---
        if (b.Src2 is (Variable or Temporary) && GetValType(b.Src2).SizeOf() == size)
        {
            LoadIntoReg(b.Src1, rd, type);
            string s2 = rs2 ?? "R18";
            if (rs2 is null) LoadIntoReg(b.Src2, "R18", type);
            Emit(mnem, rd, s2);
            if (size == 2)
            {
                // High byte carries the borrow/carry for Add/Sub (ADC/SBC).
                string highMnem = b.Op switch
                {
                    IrBinOp.Add => "ADC",
                    IrBinOp.Sub => "SBC",
                    _           => mnem,   // AND/OR/EOR are byte-independent
                };
                Emit(highMnem, GetHighReg(rd), GetHighReg(s2));
            }
            return true;
        }

        return false;
    }

    // The operand size the Div/Mod lowering runs at: src1 may be wider than dst
    // (e.g. uint16 // 10 -> uint8 reads the full 16-bit dividend), so the routine is
    // chosen from the wider of the two. Mirrors the opType computed in CompileBinary.
    private DataType DivModOpType(Binary b)
    {
        var s1 = GetValType(b.Src1);
        var d = GetValType(b.Dst);
        return s1.SizeOf() > d.SizeOf() ? s1 : d;
    }

    // Finds same-operand divide/modulo pairs in a single basic block and records the
    // fusion. Only unsigned 16-bit (the __div16 contract: quotient in R24:R25, remainder
    // in R26:R27) is fused. The pair is valid only when, between the two ops, neither the
    // dividend nor the divisor is rewritten and the second op's destination is never read
    // or written (its result is materialized early, at the primary) — and no control flow
    // intervenes, so the two run as one straight line. Mirror images (v % k before v // k
    // and v // k before v % k) are both handled.
    private void DetectDivModFusions(Function func)
    {
        _divModFuse.Clear();
        _divModSkip.Clear();
        var body = func.Body;

        bool Fusable(Binary x) =>
            x.Op is IrBinOp.Mod or IrBinOp.Div or IrBinOp.FloorDiv
            && DivModOpType(x).SizeOf() is 1 or 2   // __div8 and __div16 both yield quot+rem
            && !IsSignedComparison(x.Src1, x.Src2); // signed floor div/mod has its own routines
        static bool IsMod(IrBinOp op) => op is IrBinOp.Mod;
        static bool IsDiv(IrBinOp op) => op is IrBinOp.Div or IrBinOp.FloorDiv;

        for (int i = 0; i < body.Count; i++)
        {
            if (body[i] is not Binary p || !Fusable(p)) continue;
            if (_divModSkip.Contains(p)) continue;            // already a prior pair's secondary

            for (int j = i + 1; j < body.Count; j++)
            {
                var inst = body[j];

                if (inst is Binary s && Fusable(s)
                    && ((IsMod(p.Op) && IsDiv(s.Op)) || (IsDiv(p.Op) && IsMod(s.Op)))
                    && Equals(s.Src1, p.Src1) && Equals(s.Src2, p.Src2)
                    && !Equals(p.Dst, s.Dst))
                {
                    // Verify the second op's destination is untouched in the window; its
                    // value is stored at the primary, so a read/write between would diverge.
                    bool clean = true;
                    for (int k = i + 1; k < j && clean; k++)
                        if (TouchesVal(body[k], s.Dst, read: true)
                            || TouchesVal(body[k], s.Dst, read: false))
                            clean = false;
                    if (clean)
                    {
                        var (quot, rem) = IsMod(p.Op) ? (s.Dst, p.Dst) : (p.Dst, s.Dst);
                        _divModFuse[p] = (quot, rem);
                        _divModSkip.Add(s);
                    }
                    break;   // this primary is resolved (paired, or a blocking divmod hit)
                }

                // Only a constrained set of side-effect-free, control-flow-free instructions
                // may sit between the pair, and none may rewrite the dividend or divisor.
                if (!IsFusionTransparent(inst)
                    || TouchesVal(inst, p.Src1, read: false)
                    || TouchesVal(inst, p.Src2, read: false))
                    break;
            }
        }
    }

    // Instructions allowed between a fused divmod pair: pure data ops with no control flow
    // and no opaque memory/register clobber. A CALL, jump, label, return, indirect store,
    // or anything unrecognised ends the search (conservative).
    private static bool IsFusionTransparent(Instruction inst) => inst switch
    {
        // DebugLine is a source-line marker with no register/memory/control effect; it sits
        // between two statements (e.g. `hi = x // k` and `lo = x % k`) and must be skipped so
        // the divmod pair is still recognized — otherwise the fusion only fired for same-
        // statement pairs like the divmod() builtin.
        DebugLine => true,
        Binary or Unary or Copy or Bitcast or ArrayStore or ArrayLoad or ArrayLoadFlash
            or BytearrayLoad or FlashLoadPtr or BitCheck => true,
        _ => false,
    };

    // Conservative read/write test for the scalar Val v. Writes: the instruction's
    // destination/target equals v. Reads: v appears as a source operand. ArrayStore
    // writes array memory (never a scalar Val), so it only reads. Unhandled cases fall
    // through to false here because IsFusionTransparent already gates the instruction set.
    private static bool TouchesVal(Instruction inst, Val v, bool read)
    {
        if (read)
            return inst switch
            {
                Binary x => Equals(x.Src1, v) || Equals(x.Src2, v),
                Unary x => Equals(x.Src, v),
                Copy x => Equals(x.Src, v),
                Bitcast x => Equals(x.Src, v),
                ArrayStore x => Equals(x.Index, v) || Equals(x.Src, v),
                ArrayLoad x => Equals(x.Index, v),
                ArrayLoadFlash x => Equals(x.Index, v),
                BytearrayLoad x => Equals(x.Index, v),
                FlashLoadPtr x => Equals(x.Ptr, v) || Equals(x.Index, v),
                BitCheck x => Equals(x.Source, v),
                _ => false,
            };
        return inst switch
        {
            Binary x => Equals(x.Dst, v),
            Unary x => Equals(x.Dst, v),
            Copy x => Equals(x.Dst, v),
            Bitcast x => Equals(x.Dst, v),
            ArrayLoad x => Equals(x.Dst, v),
            ArrayLoadFlash x => Equals(x.Dst, v),
            BytearrayLoad x => Equals(x.Dst, v),
            FlashLoadPtr x => Equals(x.Dst, v),
            BitCheck x => Equals(x.Dst, v),
            _ => false,
        };
    }

    // Emits one division for a fused divide/modulo pair and stores both results.
    // 16-bit (__div16): quotient in R24:R25, remainder in R26:R27 — stash the remainder in
    // R21:R20 (the divisor R18:R19 is dead) so storing the quotient (which may use X=R26:R27)
    // cannot clobber it. 8-bit (__div8): quotient in R24, remainder in R25 — stash the
    // remainder in R19 (divisor hi byte, unused for 8-bit) before storing the quotient.
    private void EmitFusedDivMod(Binary primary, Val quotDst, Val remDst)
    {
        var opType = DivModOpType(primary);     // unsigned, gated to 8- or 16-bit in DetectDivModFusions
        LoadIntoReg(primary.Src1, "R24", opType);
        LoadIntoReg(primary.Src2, "R18", opType);
        if (opType.SizeOf() == 2)
        {
            Emit("CALL", "__div16");             // R24:R25 = quotient, R26:R27 = remainder
            Emit("MOVW", "R20", "R26");          // stash remainder in R21:R20
            StoreRegInto("R24", quotDst, opType);
            Emit("MOVW", "R24", "R20");          // remainder -> R24:R25
            StoreRegInto("R24", remDst, opType);
        }
        else
        {
            Emit("CALL", "__div8");              // R24 = quotient, R25 = remainder
            Emit("MOV", "R19", "R25");           // stash remainder (R19 = divisor hi, dead at 8-bit)
            StoreRegInto("R24", quotDst, opType);
            Emit("MOV", "R24", "R19");           // remainder -> R24
            StoreRegInto("R24", remDst, opType);
        }
    }

    // Detects the byte-pack idiom -- result = (hi << 8) <op> lo, op in {Add, BitOr} -- in a
    // single basic block and records the fusion. The shift's destination must be a 16-bit
    // value used only by the combine; the other combine operand (lo) must be a byte (so its
    // value lands entirely in the low byte with no carry/overlap). Both operand orders of a
    // commutative combine are handled. Between the two ops nothing may rewrite hi or lo, the
    // shift's result must stay untouched, and no control flow may intervene.
    private void DetectBytePackFusions(Function func)
    {
        _bytePackFuse.Clear();
        _bytePackSkip.Clear();
        var body = func.Body;

        // tmp is safe to drop only if the combine is its sole reader.
        int UsesOf(Val v)
        {
            int c = 0;
            foreach (var ins in body)
                if (TouchesVal(ins, v, read: true)) c++;
            return c;
        }

        for (int i = 0; i < body.Count; i++)
        {
            if (body[i] is not Binary sh || sh.Op != IrBinOp.LShift) continue;
            if (sh.Src2 is not Constant { Value: 8 }) continue;
            if (GetValType(sh.Dst).SizeOf() != 2) continue;          // shift result is 16-bit
            Val tmp = sh.Dst;

            for (int j = i + 1; j < body.Count; j++)
            {
                var inst = body[j];

                if (inst is Binary cmb && cmb.Op is IrBinOp.Add or IrBinOp.BitOr
                    && GetValType(cmb.Dst).SizeOf() == 2)
                {
                    Val? lo = Equals(cmb.Src1, tmp) ? cmb.Src2
                            : Equals(cmb.Src2, tmp) ? cmb.Src1 : null;
                    if (lo != null && GetValType(lo).SizeOf() == 1 && UsesOf(tmp) == 1)
                    {
                        bool clean = true;
                        for (int k = i + 1; k < j && clean; k++)
                            if (TouchesVal(body[k], tmp, read: true)
                                || TouchesVal(body[k], sh.Src1, read: false)
                                || TouchesVal(body[k], lo, read: false))
                                clean = false;
                        if (clean)
                        {
                            _bytePackFuse[cmb] = (sh.Src1, lo);
                            _bytePackSkip.Add(sh);
                        }
                    }
                    break;   // tmp's combine resolved
                }

                if (!IsFusionTransparent(inst)
                    || TouchesVal(inst, tmp, read: true)
                    || TouchesVal(inst, sh.Src1, read: false))
                    break;
            }
        }
    }

    // Emits result = (hi << 8) | lo as a direct byte placement: low byte = lo, high byte =
    // the low byte of hi (matching a 16-bit shift-by-8). hi -> R25 first so loading lo into
    // R24 cannot clobber it (neither byte source is ever homed in the R24/R25 scratch pair).
    private void EmitBytePack(Binary combine, Val hi, Val lo)
    {
        LoadIntoReg(hi, "R25", DataType.UINT8);
        LoadIntoReg(lo, "R24", DataType.UINT8);
        StoreRegInto("R24", combine.Dst, GetValType(combine.Dst));
    }

    private void CompileBinary(Binary b)
    {
        if (_divModSkip.Contains(b)) return;                 // folded into its paired __div16
        if (_divModFuse.TryGetValue(b, out var pair))
        {
            EmitFusedDivMod(b, pair.Quot, pair.Rem);
            return;
        }
        if (_bytePackSkip.Contains(b)) return;               // dead `hi << 8`, folded into the pack
        if (_bytePackFuse.TryGetValue(b, out var bp))
        {
            EmitBytePack(b, bp.Hi, bp.Lo);
            return;
        }

        DataType type = GetValType(b.Dst);
        // Delegate float operations to soft-float routines.
        if (type == DataType.FLOAT
            || GetValType(b.Src1) == DataType.FLOAT
            || GetValType(b.Src2) == DataType.FLOAT)
        {
            CompileFloatBinary(b);
            return;
        }

        // Destination-targeted in-place fast path (8-bit reg-reg / immediate arithmetic):
        // compute the result directly in dst's home register, skipping the R24 stage and
        // the store-back (and the src1 load when dst == src1). Falls back to the staged
        // path below when AVR register classes or the operand shape don't permit it.
        if (TryCompileBinaryInPlace(b)) return;
        // For Div/Mod and shift ops the source may be wider than the destination
        // (e.g. uint16 >> 8 → uint8 must operate as 16-bit to read the high byte).
        var src1Type = GetValType(b.Src1);
        var opType = (b.Op is IrBinOp.Div or IrBinOp.FloorDiv or IrBinOp.Mod
                             or IrBinOp.RShift or IrBinOp.LShift or IrBinOp.BitAnd
                             or IrBinOp.BitOr or IrBinOp.BitXor)
                     && src1Type.SizeOf() > type.SizeOf()
            ? src1Type
            : type;
        bool is16 = opType.SizeOf() == 2;
        bool is32 = opType.SizeOf() == 4;
        LoadIntoReg(b.Src1, "R24", opType);

        bool usedImm = false;
        if (b.Src2 is Constant c2)
        {
            int val = c2.Value;
            if (is32)
            {
                switch (b.Op)
                {
                    case IrBinOp.BitAnd:
                        Emit("ANDI", "R24", $"{val & 0xFF}");
                        Emit("ANDI", "R25", $"{(val >> 8) & 0xFF}");
                        Emit("ANDI", "R22", $"{(val >> 16) & 0xFF}");
                        Emit("ANDI", "R23", $"{(val >> 24) & 0xFF}");
                        usedImm = true;
                        break;
                    case IrBinOp.BitOr:
                        Emit("ORI", "R24", $"{val & 0xFF}");
                        Emit("ORI", "R25", $"{(val >> 8) & 0xFF}");
                        Emit("ORI", "R22", $"{(val >> 16) & 0xFF}");
                        Emit("ORI", "R23", $"{(val >> 24) & 0xFF}");
                        usedImm = true;
                        break;
                    case IrBinOp.RShift:
                    {
                        int byteShift = val / 8;
                        int bitShift  = val % 8;
                        bool s32 = IsSignedType(type);
                        // Sign-fill byte for the bytes vacated by a whole-byte shift (0xFF if the
                        // value is negative, else 0x00). Computed from the MSB R23 before the moves
                        // overwrite it; R26 is scratch in the constant-operand path. Without this a
                        // signed >> by 8..31 shifts in zeros (logical) instead of the sign.
                        if (s32 && byteShift >= 1) { Emit("CLR","R26"); Emit("SBRC","R23","7"); Emit("COM","R26"); }
                        if (byteShift >= 4)
                        {
                            if (s32) { Emit("MOV","R24","R26"); Emit("MOV","R25","R26"); Emit("MOV","R22","R26"); Emit("MOV","R23","R26"); }
                            else { Emit("CLR","R24"); Emit("CLR","R25"); Emit("CLR","R22"); Emit("CLR","R23"); }
                        }
                        else if (byteShift == 3) { Emit("MOV","R24","R23"); if (s32) { Emit("MOV","R25","R26"); Emit("MOV","R22","R26"); Emit("MOV","R23","R26"); } else { Emit("CLR","R25"); Emit("CLR","R22"); Emit("CLR","R23"); } }
                        else if (byteShift == 2) { Emit("MOV","R24","R22"); Emit("MOV","R25","R23"); if (s32) { Emit("MOV","R22","R26"); Emit("MOV","R23","R26"); } else { Emit("CLR","R22"); Emit("CLR","R23"); } }
                        else if (byteShift == 1) { Emit("MOV","R24","R25"); Emit("MOV","R25","R22"); Emit("MOV","R22","R23"); if (s32) Emit("MOV","R23","R26"); else Emit("CLR","R23"); }
                        for (int i = 0; i < bitShift; i++)
                        {
                            if (s32) Emit("ASR","R23"); else Emit("LSR","R23");
                            Emit("ROR","R22"); Emit("ROR","R25"); Emit("ROR","R24");
                        }
                        usedImm = true;
                        break;
                    }
                    case IrBinOp.LShift:
                    {
                        int byteShift = val / 8;
                        int bitShift  = val % 8;
                        if (byteShift >= 4) { Emit("CLR","R24"); Emit("CLR","R25"); Emit("CLR","R22"); Emit("CLR","R23"); }
                        else if (byteShift == 3) { Emit("MOV","R23","R24"); Emit("CLR","R24"); Emit("CLR","R25"); Emit("CLR","R22"); }
                        else if (byteShift == 2) { Emit("MOV","R23","R25"); Emit("MOV","R22","R24"); Emit("CLR","R24"); Emit("CLR","R25"); }
                        else if (byteShift == 1) { Emit("MOV","R23","R22"); Emit("MOV","R22","R25"); Emit("MOV","R25","R24"); Emit("CLR","R24"); }
                        for (int i = 0; i < bitShift; i++) { Emit("LSL","R24"); Emit("ROL","R25"); Emit("ROL","R22"); Emit("ROL","R23"); }
                        usedImm = true;
                        break;
                    }
                    case IrBinOp.Add:
                    {
                        int neg = -val;
                        Emit("SUBI", "R24", $"{(byte)(neg & 0xFF)}");
                        Emit("SBCI", "R25", $"{(byte)((neg >> 8) & 0xFF)}");
                        Emit("SBCI", "R22", $"{(byte)((neg >> 16) & 0xFF)}");
                        Emit("SBCI", "R23", $"{(byte)((neg >> 24) & 0xFF)}");
                        usedImm = true;
                        break;
                    }
                    case IrBinOp.Sub:
                        Emit("SUBI", "R24", $"{val & 0xFF}");
                        Emit("SBCI", "R25", $"{(val >> 8) & 0xFF}");
                        Emit("SBCI", "R22", $"{(val >> 16) & 0xFF}");
                        Emit("SBCI", "R23", $"{(val >> 24) & 0xFF}");
                        usedImm = true;
                        break;
                }
            }
            else if (!is16)
            {
                switch (b.Op)
                {
                    case IrBinOp.Add:
                        if (val == 1) Emit("INC", "R24");
                        else if (val == 255) Emit("DEC", "R24");
                        else Emit("SUBI", "R24", $"{(byte)(-val)}");
                        usedImm = true;
                        break;
                    case IrBinOp.Sub:
                        if (val == 1) Emit("DEC", "R24");
                        else if (val == 255) Emit("INC", "R24");
                        else Emit("SUBI", "R24", $"{val & 0xFF}");
                        usedImm = true;
                        break;
                    case IrBinOp.BitAnd:
                        Emit("ANDI", "R24", $"{val & 0xFF}");
                        usedImm = true;
                        break;
                    case IrBinOp.BitOr:
                        Emit("ORI", "R24", $"{val & 0xFF}");
                        usedImm = true;
                        break;
                    case IrBinOp.LShift:
                        for (int i = 0; i < (val & 7); i++) Emit("LSL", "R24");
                        usedImm = true;
                        break;
                    case IrBinOp.RShift:
                        for (int i = 0; i < (val & 7); i++)
                            if (IsSignedType(type)) Emit("ASR", "R24"); else Emit("LSR", "R24");
                        usedImm = true;
                        break;
                }
            }
            else
            {
                switch (b.Op)
                {
                    case IrBinOp.Add:
                        // ADIW R24, k is a 1-word, 2-cycle instruction for k in 1..63.
                        // SUBI+SBCI is 2 words / 4 cycles.  ADIW also handles k=0 (NOP-equivalent).
                        if (val >= 0 && val <= 63)
                            Emit("ADIW", "R24", $"{val}");
                        else if (val >= -63 && val < 0)
                            Emit("SBIW", "R24", $"{-val}");
                        else { int neg = -val; Emit("SUBI", "R24", $"{(byte)(neg & 0xFF)}"); Emit("SBCI", "R25", $"{(byte)((neg >> 8) & 0xFF)}"); }
                        usedImm = true;
                        break;
                    case IrBinOp.Sub:
                        if (val >= 0 && val <= 63)
                            Emit("SBIW", "R24", $"{val}");
                        else if (val >= -63 && val < 0)
                            Emit("ADIW", "R24", $"{-val}");
                        else { Emit("SUBI", "R24", $"{(byte)(val & 0xFF)}"); Emit("SBCI", "R25", $"{(byte)((val >> 8) & 0xFF)}"); }
                        usedImm = true;
                        break;
                    case IrBinOp.RShift:
                    {
                        int byteShift = val / 8;
                        int bitShift  = val % 8;
                        bool s16 = IsSignedType(opType);
                        if (byteShift >= 2)
                        {
                            // Shift >= 16: result is all sign bits. SBC R24,R24 leaves 0xFF/0x00
                            // from carry; both bytes must take it (a signed -1 is 0xFFFF, not 0x00FF).
                            if (s16) { Emit("MOV","R24","R25"); Emit("LSL","R24"); Emit("SBC","R24","R24"); Emit("MOV","R25","R24"); }
                            else { Emit("CLR","R24"); Emit("CLR","R25"); }
                        }
                        else if (byteShift == 1)
                        {
                            Emit("MOV","R24","R25");          // low byte := old high byte
                            // Vacated high byte must be the sign extension (0xFF if negative),
                            // not zero — otherwise a signed >> by 8..15 shifts in zeros (logical).
                            if (s16) { Emit("CLR","R25"); Emit("SBRC","R24","7"); Emit("COM","R25"); }
                            else Emit("CLR","R25");
                        }
                        for (int i = 0; i < bitShift; i++)
                        {
                            if (s16) Emit("ASR","R25"); else Emit("LSR","R25");
                            Emit("ROR","R24");
                        }
                        usedImm = true;
                        break;
                    }
                    case IrBinOp.LShift:
                    {
                        int byteShift = val / 8;
                        int bitShift  = val % 8;
                        if (byteShift >= 2) { Emit("CLR","R24"); Emit("CLR","R25"); }
                        else if (byteShift == 1) { Emit("MOV","R25","R24"); Emit("CLR","R24"); }
                        for (int i = 0; i < bitShift; i++) { Emit("LSL","R24"); Emit("ROL","R25"); }
                        usedImm = true;
                        break;
                    }
                }
            }
        }

        // Second operand. When it already lives in a home register of the matching
        // width, use that register pair directly as the ALU operand instead of staging
        // it through R18 — this drops one MOV per reg-reg arithmetic op (the codegen's
        // "stage-through-R18" pattern is otherwise pure overhead for register-resident
        // operands). Only for non-widening reg-reg ops; variable shifts clobber R18 and
        // 32-bit values have no register homes, so both keep staging.
        string s2lo = "R18", s2hi = "R19";
        if (!usedImm)
        {
            bool coalesceable = !is32
                && b.Op is IrBinOp.Add or IrBinOp.Sub or IrBinOp.BitAnd
                        or IrBinOp.BitOr or IrBinOp.BitXor
                && GetValType(b.Src2).SizeOf() == opType.SizeOf();
            string? home = coalesceable ? OperandHomeReg(b.Src2) : null;
            if (home != null) { s2lo = home; s2hi = GetHighReg(home); }
            else LoadIntoReg(b.Src2, "R18", opType);
        }

        switch (b.Op)
        {
            case IrBinOp.Add:
                if (!usedImm)
                {
                    Emit("ADD", "R24", s2lo);
                    if (is16 || is32) Emit("ADC", "R25", s2hi);
                    if (is32) { Emit("ADC", "R22", "R20"); Emit("ADC", "R23", "R21"); }
                }

                break;
            case IrBinOp.Sub:
                if (!usedImm)
                {
                    Emit("SUB", "R24", s2lo);
                    if (is16 || is32) Emit("SBC", "R25", s2hi);
                    if (is32) { Emit("SBC", "R22", "R20"); Emit("SBC", "R23", "R21"); }
                }

                break;
            case IrBinOp.BitAnd:
                if (!usedImm)
                {
                    Emit("AND", "R24", s2lo);
                    if (is16 || is32) Emit("AND", "R25", s2hi);
                    if (is32) { Emit("AND", "R22", "R20"); Emit("AND", "R23", "R21"); }
                }

                break;
            case IrBinOp.BitOr:
                if (!usedImm)
                {
                    Emit("OR", "R24", s2lo);
                    if (is16 || is32) Emit("OR", "R25", s2hi);
                    if (is32) { Emit("OR", "R22", "R20"); Emit("OR", "R23", "R21"); }
                }

                break;
            case IrBinOp.BitXor:
                Emit("EOR", "R24", s2lo);
                if (is16 || is32) Emit("EOR", "R25", s2hi);
                if (is32) { Emit("EOR", "R22", "R20"); Emit("EOR", "R23", "R21"); }
                break;
            case IrBinOp.LShift:
                if (!usedImm)
                {
                    var ls = MakeLabel("L_SHIFT_START");
                    var ld = MakeLabel("L_SHIFT_DONE");
                    EmitLabel(ls);
                    Emit("TST", "R18");
                    EmitBranch("BREQ", ld);
                    Emit("LSL", "R24");
                    if (is16 || is32) Emit("ROL", "R25");
                    if (is32) { Emit("ROL", "R22"); Emit("ROL", "R23"); }
                    Emit("DEC", "R18");
                    Emit("RJMP", ls);
                    EmitLabel(ld);
                }

                break;
            case IrBinOp.RShift:
                if (!usedImm)
                {
                    var rs = MakeLabel("L_SHIFT_START");
                    var rd = MakeLabel("L_SHIFT_DONE");
                    EmitLabel(rs);
                    Emit("TST", "R18");
                    EmitBranch("BREQ", rd);
                    if (is32)
                    {
                        if (IsSignedType(type)) Emit("ASR", "R23"); else Emit("LSR", "R23");
                        Emit("ROR", "R22");
                        Emit("ROR", "R25");
                        Emit("ROR", "R24");
                    }
                    else if (is16)
                    {
                        if (IsSignedType(type)) Emit("ASR", "R25"); else Emit("LSR", "R25");
                        Emit("ROR", "R24");
                    }
                    else
                    {
                        if (IsSignedType(type)) Emit("ASR", "R24"); else Emit("LSR", "R24");
                    }

                    Emit("DEC", "R18");
                    Emit("RJMP", rs);
                    EmitLabel(rd);
                }

                break;
            case IrBinOp.Mul:
                if (is32)
                {
                    // 32-bit product (low 32 bits) via the runtime routine; the inline 8-bit
                    // fallthrough below would otherwise multiply only the low bytes.
                    Emit("CALL", "__mul32");
                }
                else if (is16)
                {
                    // 16x16 -> 16-bit product (low 16 bits only).
                    // a = R25:R24 (hi:lo), b = R19:R18 (hi:lo).
                    if (IsSignedType(type))
                    {
                        // Signed path: MULSU requires both operands in R16-R23.
                        // R24/R25 are outside that range, so copy them to R22/R23.
                        // R22 = a_hi (copy of R25), R23 = a_lo (copy of R24).
                        Emit("MUL",   "R24", "R18");  // unsigned lo×lo -> R1:R0
                        Emit("MOV",   "R20", "R0");   // result_lo
                        Emit("MOV",   "R21", "R1");   // partial_hi
                        Emit("MOV",   "R22", "R25");  // a_hi -> R22 (within R16-R23)
                        Emit("MULSU", "R22", "R18");  // signed(a_hi) × unsigned(b_lo) -> R1:R0
                        Emit("ADD",   "R21", "R0");   // partial_hi += R0
                        Emit("MOV",   "R23", "R24");  // a_lo -> R23 (within R16-R23)
                        Emit("MULSU", "R19", "R23");  // signed(b_hi) × unsigned(a_lo) -> R1:R0
                        Emit("ADD",   "R21", "R0");   // partial_hi += R0
                        Emit("MOV",   "R24", "R20");
                        Emit("MOV",   "R25", "R21");
                    }
                    else
                    {
                        // Unsigned path: all MUL (unsigned × unsigned).
                        Emit("MUL", "R24", "R18");  // a_lo * b_lo -> R1:R0
                        Emit("MOV", "R20", "R0");   // result_lo
                        Emit("MOV", "R21", "R1");   // result_hi (partial)
                        Emit("MUL", "R24", "R19");  // a_lo * b_hi -> R1:R0
                        Emit("ADD", "R21", "R0");   // result_hi += low(a_lo*b_hi)
                        Emit("MUL", "R25", "R18");  // a_hi * b_lo -> R1:R0
                        Emit("ADD", "R21", "R0");   // result_hi += low(a_hi*b_lo)
                        Emit("MOV", "R24", "R20");
                        Emit("MOV", "R25", "R21");
                    }
                }
                else
                {
                    Emit("MUL", "R24", "R18");
                    Emit("MOV", "R24", "R0");
                }
                Emit("CLR", "R1");
                break;
            case IrBinOp.Div:
            case IrBinOp.FloorDiv:
                Emit("CALL", DivModRoutine(false, is32, is16, IsSignedComparison(b.Src1, b.Src2)));
                break;
            case IrBinOp.Mod:
                Emit("CALL", DivModRoutine(true, is32, is16, IsSignedComparison(b.Src1, b.Src2)));
                break;
            case IrBinOp.Equal:
            {
                if (!usedImm)
                {
                    Emit("CP", "R24", "R18");
                    if (is16) Emit("CPC", "R25", "R19");
                }

                var sk = MakeLabel("L_SKIP");
                Emit("LDI", "R24", "1");
                EmitBranch("BREQ", sk);
                Emit("LDI", "R24", "0");
                EmitLabel(sk);
                if (is16) Emit("LDI", "R25", "0");
                break;
            }
            case IrBinOp.NotEqual:
            {
                if (!usedImm)
                {
                    Emit("CP", "R24", "R18");
                    if (is16) Emit("CPC", "R25", "R19");
                }

                var sk = MakeLabel("L_SKIP");
                Emit("LDI", "R24", "1");
                EmitBranch("BRNE", sk);
                Emit("LDI", "R24", "0");
                EmitLabel(sk);
                if (is16) Emit("LDI", "R25", "0");
                break;
            }
            case IrBinOp.LessThan:
            {
                if (!usedImm)
                {
                    Emit("CP", "R24", "R18");
                    if (is16) Emit("CPC", "R25", "R19");
                }

                var sk = MakeLabel("L_SKIP");
                Emit("LDI", "R24", "1");
                EmitBranch(IsSignedComparison(b.Src1, b.Src2) ? "BRLT" : "BRLO", sk);
                Emit("LDI", "R24", "0");
                EmitLabel(sk);
                if (is16) Emit("LDI", "R25", "0");
                break;
            }
            case IrBinOp.GreaterEqual:
            {
                if (!usedImm)
                {
                    Emit("CP", "R24", "R18");
                    if (is16) Emit("CPC", "R25", "R19");
                }

                var sk = MakeLabel("L_SKIP");
                Emit("LDI", "R24", "1");
                EmitBranch(IsSignedComparison(b.Src1, b.Src2) ? "BRGE" : "BRSH", sk);
                Emit("LDI", "R24", "0");
                EmitLabel(sk);
                if (is16) Emit("LDI", "R25", "0");
                break;
            }
            case IrBinOp.GreaterThan:
            {
                if (!usedImm)
                {
                    Emit("CP", "R24", "R18");
                    if (is16) Emit("CPC", "R25", "R19");
                }

                var lt = MakeLabel("L_TRUE");
                var ld2 = MakeLabel("L_DONE");
                EmitBranch("BREQ", ld2);
                EmitBranch(IsSignedComparison(b.Src1, b.Src2) ? "BRGE" : "BRSH", lt);
                EmitLabel(ld2);
                Emit("LDI", "R24", "0");
                var lf = MakeLabel("L_FINAL");
                Emit("RJMP", lf);
                EmitLabel(lt);
                Emit("LDI", "R24", "1");
                EmitLabel(lf);
                if (is16) Emit("LDI", "R25", "0");
                break;
            }
            case IrBinOp.LessEqual:
            {
                if (!usedImm)
                {
                    Emit("CP", "R24", "R18");
                    if (is16) Emit("CPC", "R25", "R19");
                }

                var lt = MakeLabel("L_TRUE");
                EmitBranch(IsSignedComparison(b.Src1, b.Src2) ? "BRLT" : "BRLO", lt);
                EmitBranch("BREQ", lt);
                Emit("LDI", "R24", "0");
                var lf = MakeLabel("L_FINAL");
                Emit("RJMP", lf);
                EmitLabel(lt);
                Emit("LDI", "R24", "1");
                EmitLabel(lf);
                if (is16) Emit("LDI", "R25", "0");
                break;
            }
        }

        StoreRegInto("R24", b.Dst, type);
    }

    private void CompileBitSet(BitSet bs)
    {
        if (bs.Target is MemoryAddress { Address: >= 0x20 and <= 0x3F } mem)
        {
            Emit("SBI", $"0x{mem.Address - 0x20:X2}", $"{bs.Bit}");
            return;
        }

        LoadIntoReg(bs.Target, "R24");
        Emit("ORI", "R24", $"{1 << bs.Bit}");
        StoreRegInto("R24", bs.Target);
    }

    private void CompileBitClear(BitClear bc)
    {
        if (bc.Target is MemoryAddress { Address: >= 0x20 and <= 0x3F } mem)
        {
            Emit("CBI", $"0x{mem.Address - 0x20:X2}", $"{bc.Bit}");
            return;
        }

        LoadIntoReg(bc.Target, "R24");
        Emit("ANDI", "R24", $"{(byte)~(1 << bc.Bit)}");
        StoreRegInto("R24", bc.Target);
    }

    private void CompileBitCheck(BitCheck bck)
    {
        if (bck.Source is MemoryAddress { Address: >= 0x20 and <= 0x3F } mem)
        {
            var lF = MakeLabel("L_BIT_FALSE");
            var lD = MakeLabel("L_BIT_DONE");
            Emit("SBIS", $"0x{mem.Address - 0x20:X2}", $"{bck.Bit}");
            Emit("RJMP", lF);
            Emit("LDI", "R24", "1");
            Emit("RJMP", lD);
            EmitLabel(lF);
            Emit("LDI", "R24", "0");
            EmitLabel(lD);
            StoreRegInto("R24", bck.Dst);
            return;
        }

        LoadIntoReg(bck.Source, "R24");
        Emit("ANDI", "R24", $"{1 << bck.Bit}");
        var sk = MakeLabel("L_SKIP");
        Emit("LDI", "R18", "1");
        EmitBranch("BRNE", sk);
        Emit("LDI", "R18", "0");
        EmitLabel(sk);
        StoreRegInto("R18", bck.Dst);
    }

    private void CompileBitWrite(BitWrite bw)
    {
        if (bw.Target is MemoryAddress { Address: >= 0x20 and <= 0x3F } mem)
        {
            if (bw.Src is Constant c)
            {
                if (c.Value != 0)
                    Emit("SBI", $"0x{mem.Address - 0x20:X2}", $"{bw.Bit}");
                else
                    Emit("CBI", $"0x{mem.Address - 0x20:X2}", $"{bw.Bit}");
                return;
            }
            LoadIntoReg(bw.Src, "R24");
            var sk = MakeLabel("L_BIT_WRITE_SKIP");
            var dn = MakeLabel("L_BIT_WRITE_DONE");
            Emit("TST", "R24");
            EmitBranch("BREQ", sk);
            Emit("SBI", $"0x{mem.Address - 0x20:X2}", $"{bw.Bit}");
            Emit("RJMP", dn);
            EmitLabel(sk);
            Emit("CBI", $"0x{mem.Address - 0x20:X2}", $"{bw.Bit}");
            EmitLabel(dn);
            return;
        }

        if (bw.Src is Constant cv)
        {
            LoadIntoReg(bw.Target, "R18");
            if (cv.Value != 0)
                Emit("ORI", "R18", $"{1 << bw.Bit}");
            else
                Emit("ANDI", "R18", $"{(byte)~(1 << bw.Bit)}");
            StoreRegInto("R18", bw.Target);
            return;
        }

        LoadIntoReg(bw.Src, "R24");
        LoadIntoReg(bw.Target, "R18");
        var sk2 = MakeLabel("L_BIT_WRITE_SKIP");
        var dn2 = MakeLabel("L_BIT_WRITE_DONE");
        Emit("TST", "R24");
        EmitBranch("BREQ", sk2);
        Emit("ORI", "R18", $"{1 << bw.Bit}");
        Emit("RJMP", dn2);
        EmitLabel(sk2);
        Emit("ANDI", "R18", $"{(byte)~(1 << bw.Bit)}");
        EmitLabel(dn2);
        StoreRegInto("R18", bw.Target);
    }

    private void CompileJumpIfBitSet(JumpIfBitSet jbs)
    {
        if (jbs.Source is MemoryAddress { Address: >= 0x20 and <= 0x3F } mem)
        {
            Emit("SBIC", $"0x{mem.Address - 0x20:X2}", $"{jbs.Bit}");
            Emit("RJMP", jbs.Target);
            return;
        }

        LoadIntoReg(jbs.Source, "R24");
        Emit("ANDI", "R24", $"{1 << jbs.Bit}");
        EmitBranch("BRNE", jbs.Target);
    }

    private void CompileJumpIfBitClear(JumpIfBitClear jbc)
    {
        if (jbc.Source is MemoryAddress { Address: >= 0x20 and <= 0x3F } mem)
        {
            Emit("SBIS", $"0x{mem.Address - 0x20:X2}", $"{jbc.Bit}");
            Emit("RJMP", jbc.Target);
            return;
        }

        LoadIntoReg(jbc.Source, "R24");
        Emit("ANDI", "R24", $"{1 << jbc.Bit}");
        EmitBranch("BREQ", jbc.Target);
    }

    private void CompileAugAssign(AugAssign aa)
    {
        var type = GetValType(aa.Target);
        var is16 = type.SizeOf() == 2;
        var is32 = type.SizeOf() == 4;
        LoadIntoReg(aa.Target, "R24", type);

        var usedImm = false;
        if (aa.Operand is Constant c)
        {
            var val = c.Value;
            if (!is16)
            {
                switch (aa.Op)
                {
                    case IrBinOp.Add:
                        if (val == 1) Emit("INC", "R24");
                        else if (val == 255) Emit("DEC", "R24");
                        else Emit("SUBI", "R24", $"{(byte)(-val)}");
                        usedImm = true;
                        break;
                    case IrBinOp.Sub:
                        if (val == 1) Emit("DEC", "R24");
                        else if (val == 255) Emit("INC", "R24");
                        else Emit("SUBI", "R24", $"{val & 0xFF}");
                        usedImm = true;
                        break;
                    case IrBinOp.BitAnd:
                        Emit("ANDI", "R24", $"{val & 0xFF}");
                        usedImm = true;
                        break;
                    case IrBinOp.BitOr:
                        Emit("ORI", "R24", $"{val & 0xFF}");
                        usedImm = true;
                        break;
                    case IrBinOp.BitXor:
                        Emit("LDI", "R18", $"{val & 0xFF}");
                        Emit("EOR", "R24", "R18");
                        usedImm = true;
                        break;
                    case IrBinOp.LShift:
                        for (int i = 0; i < (val & 7); i++) Emit("LSL", "R24");
                        usedImm = true;
                        break;
                    case IrBinOp.RShift:
                        for (int i = 0; i < (val & 7); i++)
                            if (IsSignedType(type)) Emit("ASR", "R24"); else Emit("LSR", "R24");
                        usedImm = true;
                        break;
                    case IrBinOp.Mul:
                    case IrBinOp.Div:
                    case IrBinOp.FloorDiv:
                    case IrBinOp.Mod:
                    case IrBinOp.Equal:
                    case IrBinOp.NotEqual:
                    case IrBinOp.LessThan:
                    case IrBinOp.LessEqual:
                    case IrBinOp.GreaterThan:
                    case IrBinOp.GreaterEqual:
                        break;
                    default:
                        throw new ArgumentOutOfRangeException();
                }
            }
            else
            {
                switch (aa.Op)
                {
                    case IrBinOp.Add:
                        if (val >= 0 && val <= 63)
                            Emit("ADIW", "R24", $"{val}");
                        else if (val >= -63 && val < 0)
                            Emit("SBIW", "R24", $"{-val}");
                        else { var neg = -val; Emit("SUBI", "R24", $"{(byte)(neg & 0xFF)}"); Emit("SBCI", "R25", $"{(byte)((neg >> 8) & 0xFF)}"); }
                        usedImm = true;
                        break;
                    case IrBinOp.Sub:
                        if (val >= 0 && val <= 63)
                            Emit("SBIW", "R24", $"{val}");
                        else if (val >= -63 && val < 0)
                            Emit("ADIW", "R24", $"{-val}");
                        else { Emit("SUBI", "R24", $"{(byte)(val & 0xFF)}"); Emit("SBCI", "R25", $"{(byte)((val >> 8) & 0xFF)}"); }
                        usedImm = true;
                        break;
                    default:
                        Emit("LDI", "R18", $"{val & 0xFF}");
                        Emit("LDI", "R19", $"{(val >> 8) & 0xFF}");
                        break;
                }
            }
        }

        if (!usedImm) LoadIntoReg(aa.Operand, "R18", type);

        if (!usedImm)
        {
            switch (aa.Op)
            {
                case IrBinOp.Add:
                    Emit("ADD", "R24", "R18");
                    if (is16)
                        Emit("ADC", "R25", "R19");
                    break;
                case IrBinOp.Sub:
                    Emit("SUB", "R24", "R18");
                    if (is16) Emit("SBC", "R25", "R19");
                    break;
                case IrBinOp.BitAnd:
                    Emit("AND", "R24", "R18");
                    if (is16) Emit("AND", "R25", "R19");
                    break;
                case IrBinOp.BitOr:
                    Emit("OR", "R24", "R18");
                    if (is16) Emit("OR", "R25", "R19");
                    break;
                case IrBinOp.BitXor:
                    Emit("EOR", "R24", "R18");
                    if (is16) Emit("EOR", "R25", "R19");
                    break;
                case IrBinOp.LShift:
                {
                    var ls = MakeLabel("L_AUG_LSHIFT");
                    var ld = MakeLabel("L_AUG_LSHIFT_DONE");
                    EmitLabel(ls);
                    Emit("TST", "R18");
                    EmitBranch("BREQ", ld);
                    Emit("LSL", "R24");
                    if (is16) Emit("ROL", "R25");
                    Emit("DEC", "R18");
                    Emit("RJMP", ls);
                    EmitLabel(ld);
                    break;
                }
                case IrBinOp.RShift:
                {
                    var rs = MakeLabel("L_AUG_RSHIFT");
                    var rd = MakeLabel("L_AUG_RSHIFT_DONE");
                    EmitLabel(rs);
                    Emit("TST", "R18");
                    EmitBranch("BREQ", rd);
                    if (is16)
                    {
                        if (IsSignedType(type)) Emit("ASR", "R25"); else Emit("LSR", "R25");
                        Emit("ROR", "R24");
                    }
                    else
                    {
                        if (IsSignedType(type)) Emit("ASR", "R24"); else Emit("LSR", "R24");
                    }

                    Emit("DEC", "R18");
                    Emit("RJMP", rs);
                    EmitLabel(rd);
                    break;
                }
                case IrBinOp.Mul:
                    if (is32)
                    {
                        Emit("CALL", "__mul32");
                    }
                    else if (is16)
                    {
                        // a = R25:R24 (hi:lo), b = R19:R18 (hi:lo).
                        if (IsSignedType(type))
                        {
                            // Signed: MULSU requires operands in R16-R23.
                            // Copy a_hi/a_lo into R22/R23 (within range).
                            Emit("MUL",   "R24", "R18");  // unsigned lo×lo -> R1:R0
                            Emit("MOV",   "R20", "R0");
                            Emit("MOV",   "R21", "R1");
                            Emit("MOV",   "R22", "R25");  // a_hi -> R22
                            Emit("MULSU", "R22", "R18");  // signed(a_hi) × unsigned(b_lo)
                            Emit("ADD",   "R21", "R0");
                            Emit("MOV",   "R23", "R24");  // a_lo -> R23
                            Emit("MULSU", "R19", "R23");  // signed(b_hi) × unsigned(a_lo)
                            Emit("ADD",   "R21", "R0");
                            Emit("MOV",   "R24", "R20");
                            Emit("MOV",   "R25", "R21");
                        }
                        else
                        {
                            Emit("MUL", "R24", "R18");
                            Emit("MOV", "R20", "R0");
                            Emit("MOV", "R21", "R1");
                            Emit("MUL", "R24", "R19");
                            Emit("ADD", "R21", "R0");
                            Emit("MUL", "R25", "R18");
                            Emit("ADD", "R21", "R0");
                            Emit("MOV", "R24", "R20");
                            Emit("MOV", "R25", "R21");
                        }
                    }
                    else
                    {
                        Emit("MUL", "R24", "R18");
                        Emit("MOV", "R24", "R0");
                    }
                    Emit("CLR", "R1");
                    break;
                case IrBinOp.Div:
                case IrBinOp.FloorDiv:
                    Emit("CALL", DivModRoutine(false, is32, is16, IsSignedComparison(aa.Target, aa.Operand)));
                    break;
                case IrBinOp.Mod:
                    Emit("CALL", DivModRoutine(true, is32, is16, IsSignedComparison(aa.Target, aa.Operand)));
                    break;
                case IrBinOp.Equal:
                case IrBinOp.NotEqual:
                case IrBinOp.LessThan:
                case IrBinOp.LessEqual:
                case IrBinOp.GreaterThan:
                case IrBinOp.GreaterEqual:
                default: throw new Exception($"AugAssign op {aa.Op} not implemented in AVR backend");
            }
        }

        StoreRegInto("R24", aa.Target, type);
    }

    private void CompileArrayLoad(ArrayLoad al)
    {
        var elemSize = al.ElemType.SizeOf();
        var is16 = elemSize == 2;
        if (!_stackLayout.TryGetValue(al.ArrayName, out int baseOffset))
        {
            EmitComment("ArrayLoad: array not in stack_layout -- skip");
            return;
        }

        if (al.Index is Constant c)
        {
            var offset = baseOffset + c.Value * elemSize;
            if (offset < 64)
            {
                Emit("LDD", "R24", $"Y+{offset}");
                if (is16) Emit("LDD", "R25", $"Y+{offset + 1}");
            }
            else
            {
                Emit("LDS", "R24", $"0x{0x0100 + offset:X4}");
                if (is16) Emit("LDS", "R25", $"0x{0x0100 + offset + 1:X4}");
            }
        }
        else
        {
            EmitComment("ArrayLoad variable index via Z");
            LoadIntoReg(al.Index, "R24");
            if (elemSize == 2) Emit("LSL", "R24");
            var absBase = 0x0100 + baseOffset;
            Emit("LDI", "R30", $"low({absBase})");
            Emit("LDI", "R31", $"high({absBase})");
            Emit("ADD", "R30", "R24"); // Add offset to Z low byte (Generates carry if overflow)
            Emit("ADC", "R31", "R1");  // R1 == 0; avoids clobbering an R16 the allocator may hold
            Emit("LD", "R24", "Z");
            if (is16) Emit("LDD", "R25", "Z+1");
        }

        StoreRegInto("R24", al.Dst, al.ElemType);
    }

    private void CompileArrayStore(ArrayStore ast)
    {
        var elemSize = ast.ElemType.SizeOf();
        var is16 = elemSize == 2;
        if (!_stackLayout.TryGetValue(ast.ArrayName, out int baseOffset))
        {
            EmitComment("ArrayStore: array not in stack_layout -- skip");
            return;
        }

        LoadIntoReg(ast.Src, "R24", ast.ElemType);

        if (ast.Index is Constant c)
        {
            var offset = baseOffset + c.Value * elemSize;
            if (offset < 64)
            {
                Emit("STD", $"Y+{offset}", "R24");
                if (is16) Emit("STD", $"Y+{offset + 1}", "R25");
            }
            else
            {
                Emit("STS", $"0x{0x0100 + offset:X4}", "R24");
                if (is16) Emit("STS", $"0x{0x0100 + offset + 1:X4}", "R25");
            }
        }
        else
        {
            Emit("MOV", "R18", "R24");
            if (is16) Emit("MOV", "R19", "R25");
            EmitComment("ArrayStore variable index via Z");
            LoadIntoReg(ast.Index, "R24");
            if (elemSize == 2) Emit("LSL", "R24");
            var absBase = 0x0100 + baseOffset;
            Emit("LDI", "R30", $"low({absBase})");
            Emit("LDI", "R31", $"high({absBase})");
            Emit("ADD", "R30", "R24"); // Z_low = Z_low + offset (Sets Carry if overflow)
            Emit("ADC", "R31", "R1");  // R1 == 0; avoids clobbering an R16 the allocator may hold
            Emit("ST", "Z", "R18");
            if (is16) Emit("STD", "Z+1", "R19");
        }
    }

    // Load one byte from a bytearray pointer parameter: dst = ptr[index].
    private void CompileBytearrayLoad(BytearrayLoad bl)
    {
        // Load the 16-bit pointer into Z.
        // Prefer register-allocated location (higher priority than stack).
        if (_regLayout.TryGetValue(bl.PtrName, out string? baseRegL) && baseRegL != null)
        {
            Emit("MOV", "R30", baseRegL);
            Emit("MOV", "R31", GetHighReg(baseRegL));
        }
        else if (_stackLayout.TryGetValue(bl.PtrName, out int ptrOffset))
        {
            Emit("LDD", "R30", $"Y+{ptrOffset}");
            Emit("LDD", "R31", $"Y+{ptrOffset + 1}");
        }
        else
        {
            EmitComment("BytearrayLoad: pointer location unknown -- skip");
            return;
        }

        // Add index to Z, then load the byte.
        // Use ADIW for constant indices 1-63 to avoid using R16/R17 as scratch
        // (which would clobber register-allocated temporaries).
        if (bl.Index is Constant cIdx && cIdx.Value == 0)
        {
            Emit("LD", "R24", "Z");
        }
        else if (bl.Index is Constant cIdx2 && cIdx2.Value is >= 1 and <= 63)
        {
            Emit("ADIW", "R30", $"{cIdx2.Value}");
            Emit("LD", "R24", "Z");
        }
        else if (bl.Index is Constant cIdx3)
        {
            // Scratch in R26 (X-low) + R1 (zero reg), never the R16/R17 the linear-scan
            // allocator hands out -- so a register-allocated value survives this load.
            Emit("LDI", "R26", $"{cIdx3.Value}");
            Emit("ADD", "R30", "R26");
            Emit("ADC", "R31", "R1");
            Emit("LD", "R24", "Z");
        }
        else
        {
            LoadIntoReg(bl.Index, "R26");
            Emit("ADD", "R30", "R26");
            Emit("ADC", "R31", "R1");
            Emit("LD", "R24", "Z");
        }

        StoreRegInto("R24", bl.Dst, DataType.UINT8);
    }

    // Store one byte to a bytearray pointer parameter: ptr[index] = src.
    private void CompileBytearrayStore(BytearrayStore bs)
    {
        // Load source value into R18 (scratch, safe across Z setup).
        LoadIntoReg(bs.Src, "R18", DataType.UINT8);

        // Load the 16-bit pointer into Z.
        // Prefer register-allocated location (higher priority than stack).
        if (_regLayout.TryGetValue(bs.PtrName, out string? baseRegS) && baseRegS != null)
        {
            Emit("MOV", "R30", baseRegS);
            Emit("MOV", "R31", GetHighReg(baseRegS));
        }
        else if (_stackLayout.TryGetValue(bs.PtrName, out int ptrOffset))
        {
            Emit("LDD", "R30", $"Y+{ptrOffset}");
            Emit("LDD", "R31", $"Y+{ptrOffset + 1}");
        }
        else
        {
            EmitComment("BytearrayStore: pointer location unknown -- skip");
            return;
        }

        // Add index to Z, then store.
        // Use ADIW for constant indices 1-63 to avoid using R16/R17 as scratch
        // (which would clobber register-allocated temporaries).
        if (bs.Index is Constant cIdx && cIdx.Value == 0)
        {
            Emit("ST", "Z", "R18");
        }
        else if (bs.Index is Constant cIdx2 && cIdx2.Value is >= 1 and <= 63)
        {
            Emit("ADIW", "R30", $"{cIdx2.Value}");
            Emit("ST", "Z", "R18");
        }
        else if (bs.Index is Constant cIdx3)
        {
            // Scratch in R26 (X-low) + R1 (zero reg), never the R16/R17 the linear-scan
            // allocator hands out -- so a register-allocated value survives this store.
            Emit("LDI", "R26", $"{cIdx3.Value}");
            Emit("ADD", "R30", "R26");
            Emit("ADC", "R31", "R1");
            Emit("ST", "Z", "R18");
        }
        else
        {
            LoadIntoReg(bs.Index, "R26");
            Emit("ADD", "R30", "R26");
            Emit("ADC", "R31", "R1");
            Emit("ST", "Z", "R18");
        }
    }

    private static readonly Regex _asmInterpPattern =
        new(@"\{([A-Za-z_][A-Za-z0-9_]*)\}", RegexOptions.Compiled);

    // Replace {varname} placeholders in an asm() string with the actual .equ symbol
    // emitted for that Python variable. _stackLayout keys use the mangled form that is
    // already the asm symbol name: module prefix + "_" + varname (e.g. "rtos__cur_task").
    // Function names use the same scheme: "rtos__systick". We find the variable by looking
    // for a key that ends with the identifier and whose prefix is a prefix of the current
    // function name. If not found the placeholder is left unchanged (asm label, etc.).
    private string InterpolateAsmSymbols(string code)
    {
        var funcName = _currentFunction?.Name ?? "";

        return _asmInterpPattern.Replace(code, m =>
        {
            var id = m.Groups[1].Value;

            // 1. Bare lookup — main-module globals have no prefix in key
            if (_stackLayout.ContainsKey(id))
                return id;
            if (_regLayout.ContainsKey(id))
                throw new InvalidOperationException(
                    $"asm() interpolation: '{id}' is register-allocated; use a Python local to copy it first");

            // 2. Prefixed lookup — key = modulePrefix + id, e.g. "rtos_" + "_cur_task"
            //    The module prefix of the current function is the part of funcName before
            //    the function's own name, i.e. the prefix shared with the variable key.
            string? moduleMatch = null;
            string? crossModuleMatch = null;
            bool crossModuleAmbiguous = false;

            foreach (var key in _stackLayout.Keys)
            {
                if (!key.EndsWith(id, StringComparison.Ordinal)) continue;
                if (key.Length == id.Length) continue; // bare — already handled above

                var prefix = key.Substring(0, key.Length - id.Length); // e.g. "rtos_"
                if (funcName.StartsWith(prefix, StringComparison.Ordinal))
                {
                    moduleMatch = key;
                    break; // module match takes priority
                }
                if (crossModuleMatch == null)
                    crossModuleMatch = key;
                else
                    crossModuleAmbiguous = true;
            }

            if (moduleMatch != null)
                return moduleMatch;

            // Also check regLayout for same-module variables (give clear error)
            foreach (var key in _regLayout.Keys)
            {
                if (!key.EndsWith(id, StringComparison.Ordinal)) continue;
                if (key.Length == id.Length) continue;
                var prefix = key.Substring(0, key.Length - id.Length);
                if (funcName.StartsWith(prefix, StringComparison.Ordinal))
                    throw new InvalidOperationException(
                        $"asm() interpolation: '{id}' is register-allocated; use a Python local to copy it first");
            }

            // 3. Unambiguous cross-module match
            if (crossModuleMatch != null && !crossModuleAmbiguous)
                return crossModuleMatch;

            // 4. Not found — leave unchanged (ASM label, forward reference, etc.)
            return m.Value;
        });
    }

    private void CompileInlineAsmWithConstraints(InlineAsm ia)
    {
        // %N constraint substitution: load operand N into scratch register R1{6+N},
        // substitute %N in the template, emit the assembly, then store back.
        // Scratch registers: %0→R16, %1→R17, %2→R18, %3→R19 (uint8 only).
        if (ia.Operands == null || ia.Operands.Count == 0) return;
        if (ia.Operands.Count > 4)
            throw new InvalidOperationException("asm() constraint: maximum 4 operands (%0–%3)");

        var scratchRegs = new[] { "R16", "R17", "R18", "R19" };

        // Load each operand into its scratch register.
        for (int i = 0; i < ia.Operands.Count; i++)
            LoadIntoReg(ia.Operands[i], scratchRegs[i], DataType.UINT8);

        // Substitute %N → RNN in the template and emit.
        var code = ia.Code;
        for (int i = ia.Operands.Count - 1; i >= 0; i--)
            code = code.Replace($"%{i}", scratchRegs[i]);
        _assembly.Add(AvrAsmLine.MakeRaw(code));

        // Store result register back into any non-constant operand.
        for (int i = 0; i < ia.Operands.Count; i++)
        {
            if (ia.Operands[i] is not Constant)
                StoreRegInto(scratchRegs[i], ia.Operands[i], DataType.UINT8);
        }
    }

    private void CompileArrayLoadFlash(ArrayLoadFlash alf)
    {
        // Load one byte from a flash-resident const[uint8[N]] table via LPM Z.
        // Table label in flash byte-address space (same as string pool labels).
        var label = "__flash_" + alf.ArrayName.Replace('.', '_');
        LoadIntoReg(alf.Index, "R24");            // index -> R24
        Emit("LDI", "R30", $"lo8({label})");      // ZL = base byte address
        Emit("LDI", "R31", $"hi8({label})");      // ZH = base byte address
        Emit("ADD", "R30", "R24");                // Z += index (8-bit index, no overflow for small tables)
        Emit("ADC", "R31", "R1");                 // propagate carry (R1 = 0 after MUL clears)
        Emit("LPM", "R24", "Z");                  // load byte from flash
        StoreRegInto("R24", alf.Dst, DataType.UINT8);
    }

    private void CompileFlashLoadPtr(FlashLoadPtr flp)
    {
        // Dst = flash[Ptr + Index] via LPM, where Ptr is a runtime 16-bit flash byte-address
        // (e.g. a const[str] passed by reference into a non-@inline subroutine). Mirrors
        // CompileArrayLoadFlash but with a register-held base instead of a fixed label.
        LoadIntoReg(flp.Ptr, "R30", DataType.UINT16);  // Z = base flash byte-address
        LoadIntoReg(flp.Index, "R24");                 // index -> R24 (8-bit)
        Emit("ADD", "R30", "R24");                     // Z += index
        Emit("ADC", "R31", "R1");                      // propagate carry (R1 == 0)
        Emit("LPM", "R24", "Z");                       // load byte from flash
        StoreRegInto("R24", flp.Dst, DataType.UINT8);
    }

    private void EmitFlashArrayPool(TextWriter os)
    {
        if (_flashArrayPool.Count == 0) return;
        os.WriteLine();
        os.WriteLine("; --- Flash Array Pool (LPM lookup tables, const[uint8[N]]) ---");
        foreach (var (name, bytes) in _flashArrayPool)
        {
            var label = "__flash_" + name.Replace('.', '_');
            os.WriteLine($"{label}:");
            os.WriteLine("\t.byte " + string.Join(", ", bytes));
            os.WriteLine("\t.balign 2");
        }
    }

    // -------------------------------------------------------------------------
    // GC SRAM Layout
    // Emits .equ directives for shadow stack, heap-top pointer, and heap bounds.
    // Called from Compile() when program.NeedsGc is true, after all static
    // variable .equ directives have been emitted.
    //
    // Layout immediately after static variables (at _stack_base + _maxStaticUsage):
    //   _gc_ss_base       : 128 bytes  (64 shadow-stack slots x 2 bytes each)
    //   _gc_ss_top_addr   : 1 byte     (current shadow-stack depth)
    //   _gc_heap_top_lo   : 1 byte     (lo byte of GC heap write pointer)
    //   _gc_heap_top_hi   : 1 byte     (hi byte of GC heap write pointer)
    //   _heap_start       : start of GC-managed heap
    //   _heap_end         : 0x0880 (leaves ~127 bytes for hardware SP call stack)
    // -------------------------------------------------------------------------
    private void EmitGcSramLayout()
    {
        int ssBase = _maxStaticUsage;   // offset from _stack_base
        int ssTopAddr = ssBase + 128;   // 1 byte after 64-entry shadow stack
        int heapTopLo = ssTopAddr + 1;
        int heapTopHi = heapTopLo + 1;
        int heapStart = heapTopHi + 1;

        EmitRaw($"; --- GC Runtime SRAM Layout ---");
        EmitRaw($".equ _gc_ss_base,     _stack_base + {ssBase}");
        EmitRaw($".equ _gc_ss_top_addr, _stack_base + {ssTopAddr}");
        EmitRaw($".equ _gc_heap_top_lo, _stack_base + {heapTopLo}");
        EmitRaw($".equ _gc_heap_top_hi, _stack_base + {heapTopHi}");
        EmitRaw($".equ _heap_start,     _stack_base + {heapStart}");
        EmitRaw($".equ _heap_end,       0x0880");
        EmitRaw($"; Shadow stack: {ssBase}..{ssBase + 127} ({128} bytes)");
        EmitRaw($"; GC heap: {heapStart}..0x07FF (~{0x0880 - (0x0100 + heapStart)} bytes available)");
        EmitRaw("");
    }

    // -------------------------------------------------------------------------
    // GC Instruction Handlers
    // -------------------------------------------------------------------------

    // GcAlloc: call gc_alloc(size); store result GC_REF in Dst.
    // size is passed in R24 (lo) : R25 (hi) per AVR calling convention.
    // Result user_ptr is returned in R24:R25.
    private void CompileGcAlloc(GcAlloc ga)
    {
        EmitComment("gc_alloc");
        LoadIntoReg(ga.Size, "R24", DataType.UINT16);   // load size lo into R24 (hi stays R25)
        CLR_R25IfNeeded(ga.Size);                        // R25 = 0 for 1-byte sizes
        Emit("CALL", "gc_alloc");
        StoreRegInto("R24", ga.Dst, DataType.GC_REF);   // store returned user_ptr (R24:R25)
    }

    // Ensure R25 = 0 when the size operand is an 8-bit constant or UINT8 variable.
    private void CLR_R25IfNeeded(Val sizeVal)
    {
        bool is8bit = sizeVal switch
        {
            Constant c   => c.Value >= 0 && c.Value <= 255,
            Variable v   => v.Type is DataType.UINT8 or DataType.INT8,
            Temporary t  => t.Type is DataType.UINT8 or DataType.INT8,
            _            => false
        };
        if (is8bit) Emit("CLR", "R25");
    }

    private static void EmitExnRuntime(TextWriter os, HashSet<int> usedCodes, string chip)
    {
        os.WriteLine("; ── Exception runtime ──────────────────────────────────────────────────────");
        var codes = usedCodes.OrderBy(x => x).ToList();
        bool hasUart = chip is "atmega328p" or "atmega328" or "atmega168p" or "atmega168"
                              or "atmega88p" or "atmega88" or "atmega48p" or "atmega48"
                              or "atmega2560" or "atmega32u4";
        if (codes.Count == 0 || !hasUart)
        {
            os.WriteLine("__pymcu_unhandled_exn:");
            os.WriteLine("    cli");
            os.WriteLine("    rjmp .-2");
            os.WriteLine();
            return;
        }
        foreach (int code in codes)
        {
            os.WriteLine($"__exn_str_{code}:");
            os.WriteLine($"    .byte {ExnAsciiBytes(code)}");
        }
        os.WriteLine("    .balign 2");
        os.WriteLine();
        os.WriteLine("__pymcu_unhandled_exn:");
        os.WriteLine("    lds   R16, 0xC1");
        os.WriteLine("    sbrs  R16, 3");
        os.WriteLine("    rjmp  __exn_halt");
        if (codes.Count == 1)
        {
            int code = codes[0];
            os.WriteLine($"    ldi   R30, lo8(__exn_str_{code})");
            os.WriteLine($"    ldi   R31, hi8(__exn_str_{code})");
        }
        else
        {
            foreach (int code in codes)
            {
                os.WriteLine($"    cpi   R22, {code}");
                os.WriteLine($"    breq  __exn_load_{code}");
            }
            os.WriteLine("    rjmp  __exn_halt");
            for (int i = 0; i < codes.Count; i++)
            {
                int code = codes[i];
                os.WriteLine($"__exn_load_{code}:");
                os.WriteLine($"    ldi   R30, lo8(__exn_str_{code})");
                os.WriteLine($"    ldi   R31, hi8(__exn_str_{code})");
                if (i < codes.Count - 1)
                    os.WriteLine("    rjmp  __exn_print_loop");
            }
        }
        os.WriteLine("__exn_print_loop:");
        os.WriteLine("    lpm   R16, Z+");
        os.WriteLine("    tst   R16");
        os.WriteLine("    breq  __exn_halt");
        os.WriteLine("__exn_wait_udre:");
        os.WriteLine("    lds   R17, 0xC0");
        os.WriteLine("    sbrs  R17, 5");
        os.WriteLine("    rjmp  __exn_wait_udre");
        os.WriteLine("    sts   0xC6, R16");
        os.WriteLine("    rjmp  __exn_print_loop");
        os.WriteLine("__exn_halt:");
        os.WriteLine("    cli");
        os.WriteLine("    rjmp  .-2");
        os.WriteLine();
    }

    // ── Symbol shortening (release builds) ───────────────────────────────────────────────────────
    // Generates a deterministic short name for a generated symbol when it exceeds 24 characters.
    // Strategy: strip up to and including the last __ separator (Python private-name convention).
    // Fallback: FNV-1a 32-bit hash prefixed with _s for names without __ or that still collide.
    private static string MakeShortSymbol(string name)
    {
        if (name.Length <= 24) return name;
        int dunder = name.LastIndexOf("__");
        if (dunder >= 0 && dunder + 2 < name.Length)
        {
            string tail = name[(dunder + 2)..];
            if (tail.Length <= 24) return tail;
        }
        uint h = 2166136261u;
        foreach (char c in name) { h ^= (byte)c; h *= 16777619u; }
        return $"_s{h:x8}";
    }

    // Builds a full-name → short-name map for all long symbols and applies it to _assembly in-place.
    private void ApplySymbolShortening(ProgramIR program)
    {
        // Collect all long generated names: function labels + SRAM variable names.
        var longNames = new HashSet<string>();
        foreach (var fn in program.Functions)
            if (fn.Name.Length > 24) longNames.Add(fn.Name);
        foreach (var key in _stackLayout.Keys)
        {
            var underscored = key.Replace('.', '_');
            if (underscored.Length > 24) longNames.Add(underscored);
        }

        // Build the map with collision resolution.
        var map = new Dictionary<string, string>();
        var used = new HashSet<string>();
        foreach (var name in longNames.OrderByDescending(n => n.Length))
        {
            string candidate = MakeShortSymbol(name);
            int n = 0;
            while (used.Contains(candidate))
                candidate = $"{MakeShortSymbol(name)}_{++n}";
            map[name] = candidate;
            used.Add(candidate);
        }
        if (map.Count == 0) return;

        // Sort by key length descending to avoid partial-match replacements.
        var pairs = map.OrderByDescending(kv => kv.Key.Length).ToList();

        foreach (var line in _assembly)
        {
            switch (line.Type)
            {
                case AvrAsmLine.LineType.Label:
                    foreach (var (full, sh) in pairs)
                        if (line.LabelText == full) { line.LabelText = sh; break; }
                    break;
                case AvrAsmLine.LineType.Instruction:
                    foreach (var (full, sh) in pairs)
                    {
                        if (line.Op1 == full) line.Op1 = sh;
                        if (line.Op2 == full) line.Op2 = sh;
                    }
                    break;
                case AvrAsmLine.LineType.Raw:
                    foreach (var (full, sh) in pairs)
                        line.Content = line.Content.Replace(full, sh);
                    break;
            }
        }
    }

    // Returns true when varName appears as a source (read) operand in any instruction.
    // Used to skip dead parameter-store prologues for pure-asm functions.
    private static bool IsVariableReadInBody(string varName, List<Instruction> body)
    {
        static bool IsV(string n, Val v) => v is Variable vr && vr.Name == n;
        foreach (var instr in body)
        {
            bool hit = instr switch
            {
                Copy c              => IsV(varName, c.Src),
                Binary b            => IsV(varName, b.Src1) || IsV(varName, b.Src2),
                Unary u             => IsV(varName, u.Src),
                Return r            => IsV(varName, r.Value),
                Call c              => c.Args.Any(a => IsV(varName, a)),
                JumpIfZero z        => IsV(varName, z.Condition),
                JumpIfNotZero nz    => IsV(varName, nz.Condition),
                JumpIfEqual e       => IsV(varName, e.Src1) || IsV(varName, e.Src2),
                JumpIfNotEqual ne   => IsV(varName, ne.Src1) || IsV(varName, ne.Src2),
                JumpIfLessThan lt   => IsV(varName, lt.Src1) || IsV(varName, lt.Src2),
                JumpIfLessOrEqual le=> IsV(varName, le.Src1) || IsV(varName, le.Src2),
                JumpIfGreaterThan gt=> IsV(varName, gt.Src1) || IsV(varName, gt.Src2),
                JumpIfGreaterOrEqual ge => IsV(varName, ge.Src1) || IsV(varName, ge.Src2),
                JumpIfBitSet jbs    => IsV(varName, jbs.Source),
                JumpIfBitClear jbc  => IsV(varName, jbc.Source),
                BitSet bs           => IsV(varName, bs.Target),
                BitClear bc         => IsV(varName, bc.Target),
                BitWrite bw         => IsV(varName, bw.Target) || IsV(varName, bw.Src),
                BitCheck bk         => IsV(varName, bk.Source),
                AugAssign aa        => IsV(varName, aa.Target) || IsV(varName, aa.Operand),
                ArrayLoad al        => IsV(varName, al.Index),
                ArrayStore ast      => IsV(varName, ast.Index) || IsV(varName, ast.Src),
                ArrayLoadFlash alf  => IsV(varName, alf.Index),
                FlashLoadPtr flp    => IsV(varName, flp.Ptr) || IsV(varName, flp.Index),
                BytearrayLoad bl    => bl.PtrName == varName || IsV(varName, bl.Index),
                BytearrayStore bst  => bst.PtrName == varName || IsV(varName, bst.Index) || IsV(varName, bst.Src),
                LoadIndirect li     => IsV(varName, li.SrcPtr),
                StoreIndirect si    => IsV(varName, si.Src) || IsV(varName, si.DstPtr),
                Bitcast bt          => IsV(varName, bt.Src),
                IndirectCall ic     => IsV(varName, ic.FuncAddr) || ic.Args.Any(a => IsV(varName, a)),
                VirtualCall vc      => IsV(varName, vc.Self) || vc.Args.Any(a => IsV(varName, a)),
                InlineAsm ia        => ia.Operands?.Any(a => IsV(varName, a)) ?? false,
                SignalError se      => IsV(varName, se.Code),
                GcAlloc ga          => IsV(varName, ga.Size),
                _                   => false,
            };
            if (hit) return true;
        }
        return false;
    }

    private static string ExnCodeName(int code) => code switch
    {
        1 => "ValueError",
        2 => "TypeError",
        3 => "IndexError",
        4 => "KeyError",
        5 => "NotImplementedError",
        _ => $"Exception{code}"
    };

    private static string ExnAsciiBytes(int code)
    {
        string name = ExnCodeName(code);
        var bytes = new List<int> { 'E', ':' };
        foreach (char ch in name) bytes.Add(ch);
        bytes.Add(13);
        bytes.Add(10);
        bytes.Add(0);
        return string.Join(", ", bytes);
    }

    // Emit the gc_runtime.S content (embedded resource) into the output .asm file.
    private static void EmitGcRuntime(TextWriter os)
    {
        os.WriteLine();
        os.WriteLine("; --- PyMCU GC Runtime (gc_runtime.S) ---");
        var asm = System.Reflection.Assembly.GetExecutingAssembly();
        using var stream = asm.GetManifestResourceStream("gc_runtime.S")
            ?? throw new Exception("gc_runtime.S embedded resource not found in assembly");
        using var reader = new System.IO.StreamReader(stream);
        os.Write(reader.ReadToEnd());
    }

    // GcRoot: push the SRAM address of the GC_REF variable onto the shadow stack.
    // After the push, the shadow stack depth (_gc_ss_top_addr) is incremented.
    // The shadow-stack entry holds the absolute SRAM address of the local variable
    // so the GC can load and update the GC_REF value stored there.
    private void CompileGcRoot(GcRoot gr)
    {
        string varName = gr.Var switch
        {
            Variable v  => v.Name,
            Temporary t => t.Name,
            _           => throw new Exception("GcRoot: expected Variable or Temporary")
        };

        int sramAddr = GetGcRefSramAddr(varName);
        EmitComment($"gc_root push: {varName} @ 0x{sramAddr:X4}");

        // X = _gc_ss_base; index = _gc_ss_top_addr (1 byte); X += index*2
        // Then store sramAddr as 2 bytes at X; increment _gc_ss_top_addr.
        Emit("LDS",  "R16", "_gc_ss_top_addr");    // R16 = current depth
        Emit("MOV",  "R17", "R16");
        Emit("LSL",  "R17");                        // R17 = depth * 2
        Emit("LDI",  "R26", "lo8(_gc_ss_base)");
        Emit("LDI",  "R27", "hi8(_gc_ss_base)");
        Emit("CLR",  "R18");
        Emit("ADD",  "R26", "R17");
        Emit("ADC",  "R27", "R18");                 // X = _gc_ss_base + depth*2

        Emit("LDI",  "R17", $"lo8(0x{sramAddr:X4})");
        Emit("LDI",  "R18", $"hi8(0x{sramAddr:X4})");
        Emit("ST",   "X+",  "R17");                 // store sramAddr lo
        Emit("ST",   "X",   "R18");                 // store sramAddr hi

        Emit("INC",  "R16");
        Emit("STS",  "_gc_ss_top_addr", "R16");     // depth++
    }

    // GcUnroot: pop one entry from the shadow stack (decrement depth counter).
    private void CompileGcUnroot(GcUnroot gu)
    {
        EmitComment($"gc_unroot pop: {(gu.Var is Variable v ? v.Name : ((Temporary)gu.Var).Name)}");
        Emit("LDS", "R16", "_gc_ss_top_addr");
        Emit("DEC", "R16");
        Emit("STS", "_gc_ss_top_addr", "R16");
    }

    // Resolve the absolute SRAM address of a GC_REF local variable.
    private int GetGcRefSramAddr(string varName)
    {
        if (_stackLayout.TryGetValue(varName, out int offset))
            return 0x0100 + offset;
        throw new Exception($"GcRoot: variable '{varName}' not found in stack layout");
    }

    // ── Symbol map (--emit-symbols) ───────────────────────────────────────────

    /// <summary>
    /// When set, a symbols JSON file is written to this path after compilation.
    /// Format: [{"Name":"main","WordAddr":4}, ...]
    /// </summary>
    public string? EmitSymbolsPath { get; set; }

    private void WriteSymbolsIfRequested(List<AvrAsmLine> optimized, ProgramIR program)
    {
        if (string.IsNullOrEmpty(EmitSymbolsPath)) return;
        var symbols = ComputeSymbolMap(optimized, program);
        File.WriteAllText(EmitSymbolsPath,
            JsonSerializer.Serialize(symbols, AvrSymbolsJsonContext.Default.ListSymbolEntry));
    }

    private static List<SymbolEntry> ComputeSymbolMap(List<AvrAsmLine> lines, ProgramIR program)
    {
        var nonInline = program.Functions.Where(f => !f.IsInline).ToList();
        var funcNames = nonInline.Select(f => f.Name).ToHashSet();
        var funcByName = nonInline.ToDictionary(f => f.Name);

        uint word = 0;
        var result = new List<SymbolEntry>();

        foreach (var line in lines)
        {
            switch (line.Type)
            {
                case AvrAsmLine.LineType.Raw:
                    if (line.Content.StartsWith(".org ", StringComparison.Ordinal))
                    {
                        var raw = line.Content[5..].Trim();
                        word = raw.StartsWith("0x", StringComparison.OrdinalIgnoreCase)
                            ? Convert.ToUInt32(raw[2..], 16)
                            : uint.Parse(raw);
                    }
                    else
                    {
                        // asm() blocks are emitted as Raw lines but still consume flash words.
                        // Count any Raw line that looks like an instruction (not a directive or comment).
                        word += RawLineInstructionWords(line.Content);
                    }
                    break;
                case AvrAsmLine.LineType.Label:
                    if (funcNames.Contains(line.LabelText))
                    {
                        var func = funcByName[line.LabelText];
                        var displayName = !string.IsNullOrEmpty(func.OriginalName) ? func.OriginalName : line.LabelText;
                        result.Add(new SymbolEntry(displayName, word));
                    }
                    break;
                case AvrAsmLine.LineType.Instruction:
                    word += line.Mnemonic is "CALL" or "JMP" or "LDS" or "STS" ? 2u : 1u;
                    break;
            }
        }
        return result;
    }

    // 2-word AVR instructions (by mnemonic prefix, case-insensitive).
    private static readonly HashSet<string> TwoWordMnemonics =
        new(StringComparer.OrdinalIgnoreCase) { "CALL", "JMP", "LDS", "STS" };

    private static uint RawLineInstructionWords(string content)
    {
        // Strip leading whitespace and inline comments.
        var trimmed = content.TrimStart();
        if (trimmed.Length == 0) return 0;

        // Directives start with '.' or ';' (comment), sub-labels end with ':'.
        var firstChar = trimmed[0];
        if (firstChar == '.' || firstChar == ';') return 0;

        // A local label like "_dly_o16mhz:" — no instruction words.
        var spaceIdx = trimmed.IndexOf(' ');
        var mnemonic = spaceIdx >= 0 ? trimmed[..spaceIdx] : trimmed;
        if (mnemonic.EndsWith(':')) return 0;

        // Strip trailing comment from mnemonic if needed.
        var semicolonIdx = mnemonic.IndexOf(';');
        if (semicolonIdx >= 0) mnemonic = mnemonic[..semicolonIdx].TrimEnd();

        return TwoWordMnemonics.Contains(mnemonic) ? 2u : 1u;
    }

    // ── Line map (--emit-linemap) ─────────────────────────────────────────────

    /// <summary>
    /// When set, a linemap JSON file is written to this path after compilation.
    /// Format: [{"File":"src/main.py","Line":42,"WordAddr":128}, ...]
    /// Used by the debug server to map breakpoints to flash addresses.
    /// </summary>
    public string? EmitLineMapPath { get; set; }

    /// <summary>
    /// When set, a varmap JSON file is written to this path after compilation.
    /// Format: [{"Function":"fibonacci","File":"main.py","StartLine":22,"Vars":{"a":"R4","b":"R5",...}}, ...]
    /// Used by the debugger plugin to display named variables instead of raw registers.
    /// </summary>
    public string? EmitVarMapPath { get; set; }

    private void WriteLineMapIfRequested(List<AvrAsmLine> optimized)
    {
        if (string.IsNullOrEmpty(EmitLineMapPath)) return;
        var entries = ComputeLineMap(optimized);
        File.WriteAllText(EmitLineMapPath,
            JsonSerializer.Serialize(entries, AvrLineMapJsonContext.Default.ListLineMapEntry));
    }

    private static List<LineMapEntry> ComputeLineMap(List<AvrAsmLine> lines)
    {
        uint word = 0;
        var seen   = new HashSet<(string, int)>();
        var result = new List<LineMapEntry>();

        // Pending debug marker: set when a DebugMarker line is encountered, overridden by
        // subsequent markers before any instruction, flushed when the first real instruction
        // at the current word is reached. This ensures loop headers that generate only a
        // label (e.g. "while True:") never create a spurious entry that collides with the
        // first body instruction; only the innermost/last marker wins.
        (string file, int line)? pending = null;

        void Flush()
        {
            if (pending is not (var f, var l)) return;
            if (seen.Add((f, l))) result.Add(new LineMapEntry(f, l, word));
            pending = null;
        }

        foreach (var asmLine in lines)
        {
            switch (asmLine.Type)
            {
                case AvrAsmLine.LineType.Raw:
                    if (asmLine.Content.StartsWith(".org ", StringComparison.Ordinal))
                    {
                        var raw = asmLine.Content[5..].Trim();
                        word = raw.StartsWith("0x", StringComparison.OrdinalIgnoreCase)
                            ? Convert.ToUInt32(raw[2..], 16)
                            : uint.Parse(raw);
                    }
                    else
                    {
                        var w = CountRawWords(asmLine.Content);
                        if (w > 0) { Flush(); word += w; }
                    }
                    break;

                case AvrAsmLine.LineType.DebugMarker:
                    // Later markers override earlier ones at the same word position.
                    pending = (asmLine.DebugFile, asmLine.DebugLine);
                    break;

                case AvrAsmLine.LineType.Instruction:
                    Flush();
                    word += asmLine.Mnemonic is "CALL" or "JMP" or "LDS" or "STS" ? 2u : 1u;
                    break;
            }
        }
        return result;
    }

    // Counts AVR word(s) consumed by a single Raw assembly line.
    // Used by ComputeLineMap to account for inline asm bodies (asm() calls)
    // that are emitted as Raw lines instead of Instruction lines.
    private static uint CountRawWords(string content)
    {
        var trimmed = content.Trim();
        if (trimmed.Length == 0 || trimmed[0] == '.' || trimmed[0] == ';')
            return 0;
        // Strip optional "label:" prefix to isolate the instruction part.
        var colonIdx = trimmed.IndexOf(':');
        var instruction = colonIdx >= 0 ? trimmed[(colonIdx + 1)..].TrimStart() : trimmed;
        if (instruction.Length == 0) return 0;
        var spaceIdx = instruction.IndexOfAny(new[] { ' ', '\t' });
        var mnemonic = spaceIdx < 0 ? instruction : instruction[..spaceIdx];
        if (mnemonic.Length == 0 || !char.IsLetter(mnemonic[0])) return 0;
        return string.Equals(mnemonic, "CALL", StringComparison.OrdinalIgnoreCase)
            || string.Equals(mnemonic, "JMP",  StringComparison.OrdinalIgnoreCase)
            || string.Equals(mnemonic, "LDS",  StringComparison.OrdinalIgnoreCase)
            || string.Equals(mnemonic, "STS",  StringComparison.OrdinalIgnoreCase)
            ? 2u : 1u;
    }

    // SRAM base address on AVR (ATmega328P and compatible): data space starts at 0x0100.
    private const int SramBase = 0x0100;

    private void WriteVarMapIfRequested(ProgramIR program)
    {
        if (string.IsNullOrEmpty(EmitVarMapPath)) return;
        var entries = new List<VarMapEntry>();

        foreach (var func in program.Functions.Where(f => !f.IsInline))
        {
            var firstDebug = func.Body
                .OfType<DebugLine>()
                .FirstOrDefault(d => !d.IsInline && !string.IsNullOrEmpty(d.SourceFile));
            if (firstDebug is null) continue;

            var prefix       = func.Name + ".";
            var vars         = new Dictionary<string, string>(StringComparer.Ordinal);
            var varLines     = new Dictionary<string, int>(StringComparer.Ordinal);
            var stackVars    = new Dictionary<string, int>(StringComparer.Ordinal);
            var stackVarLines = new Dictionary<string, int>(StringComparer.Ordinal);
            int curLine      = firstDebug.Line;

            // --- Parameters: live from function entry. Use startLine as their VarLines
            //     entry so the debugger shows them immediately when entering the function.
            foreach (var pname in func.Params)
            {
                if (!pname.StartsWith(prefix, StringComparison.Ordinal)) continue;
                if (_regLayout.TryGetValue(pname, out var pReg))
                {
                    vars.TryAdd(pname, pReg);
                    varLines.TryAdd(pname, firstDebug.Line);
                }
                else if (_stackLayout.TryGetValue(pname, out int pOff))
                {
                    stackVars.TryAdd(pname, SramBase + pOff);
                    stackVarLines.TryAdd(pname, firstDebug.Line);
                }
            }

            // --- Locals: discovered when they first appear as an instruction destination.
            foreach (var instr in func.Body)
            {
                if (instr is DebugLine dl && !dl.IsInline && dl.Line > 0)
                    curLine = dl.Line;

                IEnumerable<Val> dsts = instr switch
                {
                    Binary b         => [b.Dst],
                    Copy c           => [c.Dst],
                    Unary u          => [u.Dst],
                    Call cl          => [cl.Dst],
                    LoadIndirect li  => [li.Dst],
                    BitCheck bc      => [bc.Dst],
                    ArrayLoad al     => [al.Dst],
                    BytearrayLoad bl => [bl.Dst],
                    _                => []
                };
                foreach (var v in dsts)
                {
                    if (v is not Variable vv) continue;
                    // Only include variables that belong to this function (not call-argument
                    // staging writes like "count_bits.v = main.n" appearing in main's body).
                    if (!vv.Name.StartsWith(prefix, StringComparison.Ordinal)) continue;

                    if (_regLayout.TryGetValue(vv.Name, out var reg))
                    {
                        vars.TryAdd(vv.Name, reg);
                        varLines.TryAdd(vv.Name, curLine);
                    }
                    else if (_stackLayout.TryGetValue(vv.Name, out int off))
                    {
                        stackVars.TryAdd(vv.Name, SramBase + off);
                        stackVarLines.TryAdd(vv.Name, curLine);
                    }
                }
            }

            // Emit an entry for every function that has a source location, even when all
            // variables are stack-spilled. Without this, functions like fibonacci (whose
            // locals all fall below the register-allocation threshold) are invisible to the
            // debugger entirely, which prevents the scope from even appearing.
            var paramNames = func.Params
                .Where(p => p.StartsWith(prefix, StringComparison.Ordinal))
                .ToList();
            entries.Add(new VarMapEntry(
                func.Name, firstDebug.SourceFile, firstDebug.Line,
                vars, varLines,
                stackVars.Count > 0 ? stackVars : null,
                stackVarLines.Count > 0 ? stackVarLines : null,
                paramNames.Count > 0 ? paramNames : null));
        }

        File.WriteAllText(EmitVarMapPath,
            JsonSerializer.Serialize(entries, AvrVarMapJsonContext.Default.ListVarMapEntry));
    }

    // -------------------------------------------------------------------------
    // T-flag error propagation (ABI interna PyMCU — reemplaza SJLJ)
    // -------------------------------------------------------------------------

    // SignalError — el callee está propagando un error al caller.
    // Emite SET: pone el T flag del SREG a 1.
    // El error code se guarda en R22 para que el dispatch en el catch site pueda
    // discriminar el tipo; R22 no es el error "carrier" (ese es T), sino el payload.
    private void CompileSignalError(SignalError se)
    {
        // Load the error code into R22 so the catch dispatcher can identify the type.
        if (se.Code is not Constant { Value: 0 })
            LoadIntoReg(se.Code, "R22", DataType.UINT8);

        if (se.CatchLabel != null)
        {
            // Local raise: the `raise` is lexically inside a `try` body in this same
            // function. Deliver the error code straight to the local catch dispatcher.
            // The dispatcher discriminates on R22 alone, so we neither set T (no caller
            // is involved) nor RET — we just jump. Leaving T untouched means a locally
            // caught raise can never leak an error to a later call site or the caller.
            // JMP (vs RJMP) is range-safe; the linker relaxes it to RJMP under -mrelax.
            Emit("JMP", se.CatchLabel);
            return;
        }

        Emit("SET");   // BSET 6 — T = 1 (signal error to caller)

        // SignalError is terminal: return immediately with T = 1.
        // CompileReturn injects CLT before RET for CanFail success paths — we must
        // bypass that by emitting RET directly here (without CLT) so T stays set.
        Emit("RET");
    }

    // SignalSuccess — el callee retorna en el happy path.
    // Emite CLT: pone el T flag a 0.
    // Se inyecta ANTES de cada RET en funciones CanFail (ver CompileReturn).
    private void CompileSignalSuccess()
    {
        Emit("CLT");   // BCLR 6 — T = 0
    }

    // BranchOnError — tras llamar a una función CanFail, ramifica si T == 1.
    // BRTS tiene un rango de ±63 bytes de PC; para targets lejanos usamos la
    // inversión BRTC/RJMP (mismo patrón que EmitBranch para los otros flags).
    private void CompileBranchOnError(BranchOnError boe)
    {
        // BRTS target  →  si T está set, salta a target.
        // EmitBranch("BRTS", target) emitiría BRTC skip; RJMP target; label skip:
        // lo que es correcto para targets fuera del rango del branch corto.
        EmitBranch("BRTS", boe.ErrorLabel);
    }
}

public record LineMapEntry(string File, int Line, uint WordAddr);

[JsonSerializable(typeof(List<LineMapEntry>))]
internal partial class AvrLineMapJsonContext : JsonSerializerContext { }

// ── Var map (--emit-varmap) ───────────────────────────────────────────────────────────────────

// StackVars/StackVarLines hold variables that are stack-spilled (not in registers).
// The int value is the absolute AVR data-space address (0x0100 + stack offset).
// Params lists parameter names so the debugger can distinguish them from first-line locals.
public record VarMapEntry(
    string Function,
    string File,
    int StartLine,
    Dictionary<string, string> Vars,
    Dictionary<string, int> VarLines,
    Dictionary<string, int>? StackVars = null,
    Dictionary<string, int>? StackVarLines = null,
    List<string>? Params = null);

[JsonSerializable(typeof(List<VarMapEntry>))]
internal partial class AvrVarMapJsonContext : JsonSerializerContext { }