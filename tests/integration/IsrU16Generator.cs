namespace PyMCU.IntegrationTests;

/// <summary>
/// Interrupt-safety with 16-bit values (codegen backlog A35). main keeps a mix of uint8 AND
/// uint16 locals live across a register-pressure loop while a Timer2 overflow ISR — itself
/// doing uint16 arithmetic — fires repeatedly. This adds the register-PAIR dimension a
/// callee-save allocator must handle: the ISR must preserve any 16-bit register pair main
/// holds live, the ISR's own 16-bit pairs/slots must be disjoint, and 16-bit SRAM slots
/// (2 bytes) must not partially alias. main's printed values must equal the ISR-free reference.
/// </summary>
public sealed class IsrU16Program
{
    public string Source { get; }
    public byte InputByte { get; }
    public IReadOnlyList<int> Expected { get; }

    private IsrU16Program(string source, byte input, List<int> expected)
        => (Source, InputByte, Expected) = (source, input, expected);

    private const int NumU8 = 6;
    private const int NumU16 = 4;
    private const int OpsPerIter = 14;
    private const int Reps = 30;

    public static IsrU16Program Generate(int seed)
    {
        var rng = new Random(seed);
        byte input = (byte)(seed * 83 + 29);
        var src = new System.Text.StringBuilder();

        int s = input;
        var u8 = new int[NumU8]; var u16 = new int[NumU16];
        var u8Op = new string[NumU8]; var u8C = new int[NumU8];
        var u16Op = new string[NumU16]; var u16C = new int[NumU16];
        string[] ops = { "+", "-", "*", "&", "|", "^" };
        for (int i = 0; i < NumU8; i++) { u8Op[i] = ops[rng.Next(ops.Length)]; u8C[i] = rng.Next(0, 256); u8[i] = ApplyU8(s, u8Op[i], u8C[i]); }
        for (int i = 0; i < NumU16; i++) { u16Op[i] = ops[rng.Next(ops.Length)]; u16C[i] = rng.Next(0, 65536); u16[i] = ApplyU16(s, u16Op[i], u16C[i]); }

        var stmts = new List<(string Src, Action Apply)>();
        for (int n = 0; n < OpsPerIter; n++)
        {
            if (rng.Next(2) == 0)
            {
                int k = rng.Next(NumU8), a = rng.Next(NumU8); string op = ops[rng.Next(ops.Length)];
                if (rng.Next(2) == 0) { int b = rng.Next(NumU8); stmts.Add(($"a{k} = a{a} {op} a{b}", () => u8[k] = ApplyU8(u8[a], op, u8[b]))); }
                else { int c = rng.Next(0, 256); stmts.Add(($"a{k} = a{a} {op} {c}", () => u8[k] = ApplyU8(u8[a], op, c))); }
            }
            else
            {
                int k = rng.Next(NumU16), a = rng.Next(NumU16); string op = ops[rng.Next(ops.Length)];
                if (rng.Next(2) == 0) { int b = rng.Next(NumU16); stmts.Add(($"b{k} = b{a} {op} b{b}", () => u16[k] = ApplyU16(u16[a], op, u16[b]))); }
                else { int c = rng.Next(0, 65536); stmts.Add(($"b{k} = b{a} {op} {c}", () => u16[k] = ApplyU16(u16[a], op, c))); }
            }
        }

        int ia = rng.Next(0, 65536), ib = rng.Next(0, 65536);

        src.Append("from pymcu.types import uint8, uint16, interrupt, asm\n");
        src.Append("from pymcu.chips.atmega328p import TCCR2B, TIMSK2\n");
        src.Append("from pymcu.hal.uart import UART\n\n\n");
        src.Append("acc16: uint16 = 0\n\n\n");
        // The ISR does 16-bit arithmetic → register pairs + ADD/ADC, EOR byte-wise, etc.
        src.Append("@interrupt(0x0012)\n");
        src.Append("def t2_ovf():\n");
        src.Append("    global acc16\n");
        src.Append($"    w0: uint16 = acc16 + {ia}\n");
        src.Append($"    w1: uint16 = w0 ^ {ib}\n");
        src.Append("    acc16 = w1 * 3\n\n\n");

        src.Append("def main():\n");
        src.Append("    uart = UART(9600)\n");
        src.Append("    uart.println(\"GO\")\n");
        src.Append("    TCCR2B.value = 0x03\n    TIMSK2.value = 0x01\n");
        src.Append("    asm(\"SEI\")\n");
        src.Append("    s: uint8 = uart.read_blocking()\n");
        for (int i = 0; i < NumU8; i++) src.Append($"    a{i}: uint8 = s {u8Op[i]} {u8C[i]}\n");
        for (int i = 0; i < NumU16; i++) src.Append($"    b{i}: uint16 = uint16(s) {u16Op[i]} {u16C[i]}\n");
        src.Append("    r: uint8 = 0\n");
        src.Append($"    while r < {Reps}:\n");
        foreach (var (line, _) in stmts) src.Append($"        {line}\n");
        src.Append("        r = r + 1\n");

        for (int rep = 0; rep < Reps; rep++)
            foreach (var (_, apply) in stmts) apply();

        var expected = new List<int>();
        for (int i = 0; i < NumU8; i++) { src.Append($"    print(a{i})\n"); expected.Add(u8[i]); }
        for (int i = 0; i < NumU16; i++) { src.Append($"    print(b{i})\n"); expected.Add(u16[i]); }
        src.Append("    while True:\n        pass\n");

        return new IsrU16Program(src.ToString(), input, expected);
    }

    private static int ApplyU8(int a, string op, int b) => op switch
    {
        "+" => (a + b) & 0xFF, "-" => (a - b) & 0xFF, "*" => (a * b) & 0xFF,
        "&" => (a & b) & 0xFF, "|" => (a | b) & 0xFF, "^" => (a ^ b) & 0xFF,
        _ => throw new ArgumentException(op),
    };

    private static int ApplyU16(int a, string op, int b) => op switch
    {
        "+" => (a + b) & 0xFFFF, "-" => (a - b) & 0xFFFF, "*" => (a * b) & 0xFFFF,
        "&" => (a & b) & 0xFFFF, "|" => (a | b) & 0xFFFF, "^" => (a ^ b) & 0xFFFF,
        _ => throw new ArgumentException(op),
    };
}
