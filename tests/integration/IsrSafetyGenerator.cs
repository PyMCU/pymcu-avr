namespace PyMCU.IntegrationTests;

/// <summary>
/// Generator of interrupt-safety stress programs (codegen backlog A33). Each seed yields a
/// program where:
///   * a Timer2 overflow ISR fires very frequently (prescaler 1 → every 256 cycles) while
///     main runs, and does its own multi-step arithmetic on an ISR-owned global — using
///     registers, so it must save/restore everything it touches;
///   * main reads a runtime seed byte, derives a pool of uint8 locals, and repeatedly applies
///     a fixed sequence of arithmetic/bitwise statements in a loop (so the ISR interrupts it
///     mid-computation, with many values live), then prints the final values.
///
/// The ISR's work is independent of main's locals, so a correct context save/restore leaves
/// main's printed values exactly equal to the ISR-free reference computed here. If the ISR
/// (now, or after the planned callee-save allocator change) fails to preserve a register that
/// main has live, a printed value diverges — exactly the property the redesign must keep.
/// </summary>
public sealed class IsrSafetyProgram
{
    public string Source { get; }
    public byte InputByte { get; }
    public IReadOnlyList<int> Expected { get; }

    private IsrSafetyProgram(string source, byte input, List<int> expected)
        => (Source, InputByte, Expected) = (source, input, expected);

    private const int NumU8 = 8;
    private const int OpsPerIter = 14;
    private const int Reps = 40;   // loop iterations: long enough for many ISR firings

    public static IsrSafetyProgram Generate(int seed)
    {
        var rng = new Random(seed);
        byte input = (byte)(seed * 53 + 7);
        var src = new System.Text.StringBuilder();

        int s = input;
        var v = new int[NumU8];
        var initOp = new string[NumU8]; var initC = new int[NumU8];
        string[] ops = { "+", "-", "*", "&", "|", "^" };
        for (int i = 0; i < NumU8; i++) { initOp[i] = ops[rng.Next(ops.Length)]; initC[i] = rng.Next(0, 256); v[i] = ApplyU8(s, initOp[i], initC[i]); }

        // Build the per-iteration statement list once (source line + reference action).
        var stmts = new List<(string Src, Action Apply)>();
        for (int n = 0; n < OpsPerIter; n++)
        {
            int k = rng.Next(NumU8), a = rng.Next(NumU8);
            int kind = rng.Next(0, 3);
            if (kind == 0) { int b = rng.Next(NumU8); string op = ops[rng.Next(ops.Length)]; stmts.Add(($"v{k} = v{a} {op} v{b}", () => v[k] = ApplyU8(v[a], op, v[b]))); }
            else if (kind == 1) { int c = rng.Next(0, 256); string op = ops[rng.Next(ops.Length)]; stmts.Add(($"v{k} = v{a} {op} {c}", () => v[k] = ApplyU8(v[a], op, c))); }
            else { int sh = rng.Next(0, 8); bool left = rng.Next(2) == 0; stmts.Add(($"v{k} = v{a} {(left ? "<<" : ">>")} {sh}", () => v[k] = (left ? (v[a] << sh) : (v[a] >> sh)) & 0xFF)); }
        }

        // ISR constants (its own state; independent of main's locals).
        int ia = rng.Next(0, 256), ib = rng.Next(0, 256), ic = rng.Next(0, 256);

        // ── Source ─────────────────────────────────────────────────────────
        src.Append("from pymcu.types import uint8, interrupt, asm\n");
        src.Append("from pymcu.chips.atmega328p import TCCR2B, TIMSK2\n");
        src.Append("from pymcu.hal.uart import UART\n\n\n");
        src.Append("acc: uint8 = 0\n\n\n");
        // Timer2 overflow vector = byte 0x0012. The ISR uses several locals so the codegen
        // (and any future callee-save allocator) must preserve them around main's live values.
        src.Append("@interrupt(0x0012)\n");
        src.Append("def t2_ovf():\n");
        src.Append("    global acc\n");
        src.Append($"    w0: uint8 = acc + {ia}\n");
        src.Append($"    w1: uint8 = w0 * 5\n");
        src.Append($"    w2: uint8 = w1 ^ {ib}\n");
        src.Append($"    acc = w2 - {ic}\n\n\n");

        src.Append("def main():\n");
        src.Append("    uart = UART(9600)\n");
        src.Append("    uart.println(\"GO\")\n");
        src.Append("    TCCR2B.value = 0x01\n");   // Timer2 prescaler 1 → overflow every 256 cycles
        src.Append("    TIMSK2.value = 0x01\n");   // TOIE2: enable overflow interrupt
        src.Append("    asm(\"SEI\")\n");
        src.Append("    s: uint8 = uart.read_blocking()\n");
        for (int i = 0; i < NumU8; i++) src.Append($"    v{i}: uint8 = s {initOp[i]} {initC[i]}\n");
        src.Append($"    r: uint8 = 0\n");
        src.Append($"    while r < {Reps}:\n");
        foreach (var (line, _) in stmts) src.Append($"        {line}\n");
        src.Append("        r = r + 1\n");

        // ── Reference: apply the loop body Reps times ──────────────────────
        for (int rep = 0; rep < Reps; rep++)
            foreach (var (_, apply) in stmts) apply();

        var expected = new List<int>();
        for (int i = 0; i < NumU8; i++) { src.Append($"    print(v{i})\n"); expected.Add(v[i]); }
        src.Append("    while True:\n        pass\n");

        return new IsrSafetyProgram(src.ToString(), input, expected);
    }

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
}
