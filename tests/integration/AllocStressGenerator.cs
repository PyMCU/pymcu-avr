namespace PyMCU.IntegrationTests;

/// <summary>
/// Deterministic generator of register-allocator stress programs for property/differential
/// testing. Each seed yields a self-contained PyMCU program that:
///   * reads ONE seed byte at runtime (<c>uart.read_blocking()</c>) — so every value is a
///     runtime value the optimizer cannot constant-fold, which is what makes the program
///     actually exercise the register allocator rather than fold to printed constants,
///   * derives a pool of uint8 and uint16 locals from that seed (more than the register file
///     holds, forcing allocation / spilling decisions),
///   * runs a straight-line sequence of arithmetic/bitwise statements over those locals
///     (so a clobbered or mis-homed register yields a wrong value),
///   * threads several values through real (non-@inline) function calls, exercising the
///     call-spanning case allocator bugs historically broke (a value read as 0), and
///   * prints every final value, one decimal per line.
///
/// The generator simultaneously evaluates the same operations in C# with PyMCU's exact
/// fixed-width wrapping semantics (no integer promotion: <c>uint8 OP uint8</c> wraps to
/// uint8) for the injected seed byte, producing the <see cref="Expected"/> oracle. A mismatch
/// between the simulated output and Expected is a codegen/allocator bug. This is the safety
/// net to run before and after the register-allocator redesign (codegen backlog A17/A31).
/// </summary>
public sealed class AllocStressProgram
{
    public string Source { get; }
    /// <summary>The seed byte the test must inject over UART after the "GO" banner.</summary>
    public byte InputByte { get; }
    /// <summary>Expected printed decimals, in print order.</summary>
    public IReadOnlyList<int> Expected { get; }

    private AllocStressProgram(string source, byte input, List<int> expected)
        => (Source, InputByte, Expected) = (source, input, expected);

    private const int NumU8 = 12;   // exceeds the R4-R15 (12) + R16/R17 register budget
    private const int NumU16 = 6;
    private const int NumStmts = 44;
    private const int NumHelpers = 3;

    public static AllocStressProgram Generate(int seed)
    {
        var rng = new Random(seed);
        byte input = (byte)(seed * 37 + 11);   // the runtime seed byte the test injects
        var src = new System.Text.StringBuilder();

        // ── Reference state (computed from the injected seed byte `s`) ─────
        int s = input;
        var u8 = new int[NumU8];
        var u16 = new int[NumU16];

        // Per-var initializer recipe derived from `s` (runtime → not constant-folded).
        var u8InitOp = new string[NumU8]; var u8InitC = new int[NumU8];
        var u16InitOp = new string[NumU16]; var u16InitC = new int[NumU16];
        string[] initOps = { "+", "-", "*", "^", "&", "|" };
        for (int i = 0; i < NumU8; i++) { u8InitOp[i] = initOps[rng.Next(initOps.Length)]; u8InitC[i] = rng.Next(0, 256); u8[i] = ApplyU8(s, u8InitOp[i], u8InitC[i]); }
        for (int i = 0; i < NumU16; i++) { u16InitOp[i] = initOps[rng.Next(initOps.Length)]; u16InitC[i] = rng.Next(0, 65536); u16[i] = ApplyU16(s, u16InitOp[i], u16InitC[i]); }

        // Three helpers of deliberately varied SHAPE, to exercise the paths a callee-save
        // register allocator must get right (codegen backlog A33):
        //   h0 — leaf, single return.
        //   h1 — MULTIPLE return paths (each exit must restore any callee-saved register).
        //   h2 — NESTED call (calls h0): h0's frame must preserve h2's live values, and h2's
        //        live values must survive across the call to h0.
        // Constants are fixed per seed; the reference Helper() below mirrors each exactly.
        var hA = new int[NumHelpers]; var hB = new int[NumHelpers];
        for (int k = 0; k < NumHelpers; k++) { hA[k] = rng.Next(0, 256); hB[k] = rng.Next(0, 256); }
        int Helper(int k, int x) => k switch
        {
            0 => (((x + hA[0]) & 0xFF) ^ hB[0]) & 0xFF,
            1 => (x & 0x40) != 0 ? (x + hA[1]) & 0xFF : (x ^ hB[1]) & 0xFF,
            _ => x > 0x80 ? (Helper(0, x) - hA[2]) & 0xFF : (Helper(0, x) + hB[2]) & 0xFF,
        };

        // ── Source: helpers (non-@inline → real CALL → call-spanning) ──────
        src.Append("from pymcu.types import uint8, uint16\n");
        src.Append("from pymcu.hal.uart import UART\n\n\n");
        src.Append($"def h0(x: uint8) -> uint8:\n    return ((x + {hA[0]}) ^ {hB[0]})\n\n\n");
        src.Append("def h1(x: uint8) -> uint8:\n");
        src.Append($"    if (x & 64) > 0:\n        return x + {hA[1]}\n    return x ^ {hB[1]}\n\n\n");
        src.Append("def h2(x: uint8) -> uint8:\n");
        src.Append($"    if x > 128:\n        return h0(x) - {hA[2]}\n    return h0(x) + {hB[2]}\n\n\n");

        src.Append("def main():\n");
        src.Append("    uart = UART(9600)\n");
        src.Append("    uart.println(\"GO\")\n");
        src.Append("    s: uint8 = uart.read_blocking()\n");
        for (int i = 0; i < NumU8; i++) src.Append($"    a{i}: uint8 = s {u8InitOp[i]} {u8InitC[i]}\n");
        for (int i = 0; i < NumU16; i++) src.Append($"    b{i}: uint16 = uint16(s) {u16InitOp[i]} {u16InitC[i]}\n");

        // ── Statement sequence ─────────────────────────────────────────────
        string[] ops = { "+", "-", "*", "&", "|", "^" };
        for (int stmt = 0; stmt < NumStmts; stmt++)
        {
            int choice = rng.Next(0, 10);
            if (choice < 4)
            {
                int k = rng.Next(NumU8), a = rng.Next(NumU8);
                string op = ops[rng.Next(ops.Length)];
                if (rng.Next(2) == 0) { int b = rng.Next(NumU8); u8[k] = ApplyU8(u8[a], op, u8[b]); src.Append($"    a{k} = a{a} {op} a{b}\n"); }
                else { int c = rng.Next(0, 256); u8[k] = ApplyU8(u8[a], op, c); src.Append($"    a{k} = a{a} {op} {c}\n"); }
            }
            else if (choice < 6)
            {
                int k = rng.Next(NumU8), a = rng.Next(NumU8), sh = rng.Next(0, 8);
                if (rng.Next(2) == 0) { u8[k] = (u8[a] << sh) & 0xFF; src.Append($"    a{k} = a{a} << {sh}\n"); }
                else { u8[k] = (u8[a] >> sh) & 0xFF; src.Append($"    a{k} = a{a} >> {sh}\n"); }
            }
            else if (choice < 8)
            {
                int k = rng.Next(NumU16), a = rng.Next(NumU16);
                string op = ops[rng.Next(ops.Length)];
                if (rng.Next(2) == 0) { int b = rng.Next(NumU16); u16[k] = ApplyU16(u16[a], op, u16[b]); src.Append($"    b{k} = b{a} {op} b{b}\n"); }
                else { int c = rng.Next(0, 65536); u16[k] = ApplyU16(u16[a], op, c); src.Append($"    b{k} = b{a} {op} {c}\n"); }
            }
            else
            {
                int k = rng.Next(NumU8), a = rng.Next(NumU8), j = rng.Next(NumHelpers);
                u8[k] = Helper(j, u8[a]);
                src.Append($"    a{k} = h{j}(a{a})\n");
            }
        }

        // ── Print every final value (one decimal per line) ─────────────────
        var expected = new List<int>();
        for (int i = 0; i < NumU8; i++) { src.Append($"    print(a{i})\n"); expected.Add(u8[i]); }
        for (int i = 0; i < NumU16; i++) { src.Append($"    print(b{i})\n"); expected.Add(u16[i]); }

        src.Append("    while True:\n        pass\n");
        return new AllocStressProgram(src.ToString(), input, expected);
    }

    // PyMCU fixed-width semantics: the operation is performed at the operand width and the
    // result wraps to that width (no C-style integer promotion).
    private static int ApplyU8(int a, string op, int b) => op switch
    {
        "+" => (a + b) & 0xFF,
        "-" => (a - b) & 0xFF,
        "*" => (a * b) & 0xFF,
        "&" => (a & b) & 0xFF,
        "|" => (a | b) & 0xFF,
        "^" => (a ^ b) & 0xFF,
        _ => throw new ArgumentException(op),
    };

    private static int ApplyU16(int a, string op, int b) => op switch
    {
        "+" => (a + b) & 0xFFFF,
        "-" => (a - b) & 0xFFFF,
        "*" => (a * b) & 0xFFFF,
        "&" => (a & b) & 0xFFFF,
        "|" => (a | b) & 0xFFFF,
        "^" => (a ^ b) & 0xFFFF,
        _ => throw new ArgumentException(op),
    };
}
