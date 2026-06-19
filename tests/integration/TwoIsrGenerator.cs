namespace PyMCU.IntegrationTests;

/// <summary>
/// Like <see cref="IsrSafetyProgram"/> but with TWO interrupt handlers (Timer0 and Timer2
/// overflow), each with its own locals, both firing while main runs a register-pressure loop.
/// Validates that the stack allocator gives each ISR a region disjoint from main AND from the
/// other ISR (the per-ISR base increment, codegen backlog A34): an overlap of the two ISRs'
/// slots, or of either with main's, would perturb main's deterministic printed values.
/// </summary>
public sealed class TwoIsrProgram
{
    public string Source { get; }
    public byte InputByte { get; }
    public IReadOnlyList<int> Expected { get; }

    private TwoIsrProgram(string source, byte input, List<int> expected)
        => (Source, InputByte, Expected) = (source, input, expected);

    private const int NumU8 = 8;
    private const int OpsPerIter = 12;
    private const int Reps = 40;

    public static TwoIsrProgram Generate(int seed)
    {
        var rng = new Random(seed);
        byte input = (byte)(seed * 61 + 17);
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

        int a0 = rng.Next(0, 256), b0 = rng.Next(0, 256);
        int a1 = rng.Next(0, 256), b1 = rng.Next(0, 256);

        src.Append("from pymcu.types import uint8, interrupt, asm\n");
        src.Append("from pymcu.chips.atmega328p import TCCR0B, TIMSK0, TCCR2B, TIMSK2\n");
        src.Append("from pymcu.hal.uart import UART\n\n\n");
        src.Append("acc0: uint8 = 0\nacc1: uint8 = 0\n\n\n");
        // Timer0 overflow → vector 0x0020.
        src.Append("@interrupt(0x0020)\n");
        src.Append("def t0_ovf():\n");
        src.Append("    global acc0\n");
        src.Append($"    p0: uint8 = acc0 + {a0}\n");
        src.Append($"    p1: uint8 = p0 * 3\n");
        src.Append($"    acc0 = p1 ^ {b0}\n\n\n");
        // Timer2 overflow → vector 0x0012.
        src.Append("@interrupt(0x0012)\n");
        src.Append("def t2_ovf():\n");
        src.Append("    global acc1\n");
        src.Append($"    q0: uint8 = acc1 ^ {a1}\n");
        src.Append($"    q1: uint8 = q0 + 7\n");
        src.Append($"    acc1 = q1 * 5\n\n\n");

        src.Append("def main():\n");
        src.Append("    uart = UART(9600)\n");
        src.Append("    uart.println(\"GO\")\n");
        src.Append("    TCCR0B.value = 0x01\n    TIMSK0.value = 0x01\n");   // Timer0 prescaler 1, TOIE0
        src.Append("    TCCR2B.value = 0x01\n    TIMSK2.value = 0x01\n");   // Timer2 prescaler 1, TOIE2
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

        return new TwoIsrProgram(src.ToString(), input, expected);
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
