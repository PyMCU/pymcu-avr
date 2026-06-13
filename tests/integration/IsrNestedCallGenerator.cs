namespace PyMCU.IntegrationTests;

/// <summary>
/// Interrupt-safety with a NESTED CALL inside the ISR (codegen backlog A34/A35). The ISR calls
/// a dedicated helper (entered in interrupt context) and keeps a local value live ACROSS that
/// call. This exercises two things the allocator must get right when an ISR can preempt main:
///   * the ISR's callee tree gets a stack region disjoint from main — the call-tree DFS that
///     allocates an ISR's region must follow into its callees, or the helper's slots alias
///     main's locals;
///   * the ISR's own call-spanning local must survive the nested call (saved/restored like any
///     callee-clobbered register), independently of main's live values.
/// main runs a register-pressure loop; its printed values must equal the ISR-free reference.
/// </summary>
public sealed class IsrNestedCallProgram
{
    public string Source { get; }
    public byte InputByte { get; }
    public IReadOnlyList<int> Expected { get; }

    private IsrNestedCallProgram(string source, byte input, List<int> expected)
        => (Source, InputByte, Expected) = (source, input, expected);

    private const int NumU8 = 8;
    private const int OpsPerIter = 12;
    private const int Reps = 40;

    public static IsrNestedCallProgram Generate(int seed)
    {
        var rng = new Random(seed);
        byte input = (byte)(seed * 71 + 23);
        var src = new System.Text.StringBuilder();

        int s = input;
        var v = new int[NumU8];
        var initOp = new string[NumU8]; var initC = new int[NumU8];
        string[] ops = { "+", "-", "*", "&", "|", "^" };
        for (int i = 0; i < NumU8; i++) { initOp[i] = ops[rng.Next(ops.Length)]; initC[i] = rng.Next(0, 256); v[i] = ApplyU8(s, initOp[i], initC[i]); }

        var stmts = new List<(string Src, Action Apply)>();
        for (int n = 0; n < OpsPerIter; n++)
        {
            int k = rng.Next(NumU8), a = rng.Next(NumU8);
            if (rng.Next(2) == 0) { int b = rng.Next(NumU8); string op = ops[rng.Next(ops.Length)]; stmts.Add(($"v{k} = v{a} {op} v{b}", () => v[k] = ApplyU8(v[a], op, v[b]))); }
            else { int c = rng.Next(0, 256); string op = ops[rng.Next(ops.Length)]; stmts.Add(($"v{k} = v{a} {op} {c}", () => v[k] = ApplyU8(v[a], op, c))); }
        }

        int ha = rng.Next(0, 256), hb = rng.Next(0, 256), ic = rng.Next(0, 256);

        src.Append("from pymcu.types import uint8, interrupt, asm\n");
        src.Append("from pymcu.chips.atmega328p import TCCR2B, TIMSK2\n");
        src.Append("from pymcu.hal.uart import UART\n\n\n");
        src.Append("acc: uint8 = 0\n\n\n");
        // Helper called ONLY from the ISR → runs in interrupt context. Its locals must be
        // disjoint from main's frame.
        src.Append("def isr_helper(x: uint8) -> uint8:\n");
        src.Append($"    h0: uint8 = x + {ha}\n");
        src.Append($"    h1: uint8 = h0 * 3\n");
        src.Append($"    return h1 ^ {hb}\n\n\n");
        src.Append("@interrupt(0x0012)\n");
        src.Append("def t2_ovf():\n");
        src.Append("    global acc\n");
        src.Append($"    t: uint8 = acc + {ic}\n");          // t is live across the nested call
        src.Append("    acc = isr_helper(t) + t\n\n\n");

        src.Append("def main():\n");
        src.Append("    uart = UART(9600)\n");
        src.Append("    uart.println(\"GO\")\n");
        src.Append("    TCCR2B.value = 0x01\n    TIMSK2.value = 0x01\n");
        src.Append("    asm(\"SEI\")\n");
        src.Append("    s: uint8 = uart.read_blocking()\n");
        for (int i = 0; i < NumU8; i++) src.Append($"    v{i}: uint8 = s {initOp[i]} {initC[i]}\n");
        src.Append("    r: uint8 = 0\n");
        src.Append($"    while r < {Reps}:\n");
        foreach (var (line, _) in stmts) src.Append($"        {line}\n");
        src.Append("        r = r + 1\n");

        for (int rep = 0; rep < Reps; rep++)
            foreach (var (_, apply) in stmts) apply();

        var expected = new List<int>();
        for (int i = 0; i < NumU8; i++) { src.Append($"    print(v{i})\n"); expected.Add(v[i]); }
        src.Append("    while True:\n        pass\n");

        return new IsrNestedCallProgram(src.ToString(), input, expected);
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
