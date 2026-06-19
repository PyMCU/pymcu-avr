namespace PyMCU.IntegrationTests;

/// <summary>
/// Signed counterpart of <see cref="AllocStressProgram"/>: deterministic property/differential
/// programs over a pool of int8/int16 locals, exercising the full signed operator set
/// (+ - * & | ^, floor // and %, arithmetic >> and <<, plus a call-spanning signed helper).
/// Each seed's program is also evaluated in C# with PyMCU's fixed-width *signed* semantics
/// (two's-complement wrap to the operand width; // and % follow Python's floor/divisor-signed
/// rules) to form the oracle. A mismatch is a signed-codegen or allocator bug.
///
/// This is the systematic fidelity check for signed arithmetic — the area that historically
/// fell back to unsigned routines (codegen backlog A37/A38). Values come from a runtime seed
/// byte so nothing constant-folds; // and % always use a non-zero constant divisor so no
/// runtime division-by-zero can occur.
/// </summary>
public sealed class SignedStressProgram
{
    public string Source { get; }
    public byte InputByte { get; }
    public IReadOnlyList<int> Expected { get; }

    private SignedStressProgram(string source, byte input, List<int> expected)
        => (Source, InputByte, Expected) = (source, input, expected);

    private const int NumI8 = 10;
    private const int NumI16 = 5;
    private const int NumStmts = 46;

    // Non-zero divisors (both signs) for // and %.
    private static readonly int[] Div8 = { 1, -1, 2, -2, 3, -3, 7, -7, 10, -10, 5, -5 };
    private static readonly int[] Div16 = { 1, -1, 2, -2, 3, -3, 7, -7, 100, -100, 1000, -1000 };

    public static SignedStressProgram Generate(int seed)
    {
        var rng = new Random(seed);
        byte input = (byte)(seed * 53 + 17);
        int s = input;
        int s8 = (sbyte)input;                 // seed reinterpreted as signed int8

        var i8 = new int[NumI8];
        var i16 = new int[NumI16];
        var src = new System.Text.StringBuilder();

        string[] initOps = { "+", "-", "*", "^", "&", "|" };
        var i8Op = new string[NumI8]; var i8C = new int[NumI8];
        var i16Op = new string[NumI16]; var i16C = new int[NumI16];
        for (int i = 0; i < NumI8; i++) { i8Op[i] = initOps[rng.Next(initOps.Length)]; i8C[i] = rng.Next(-128, 128); i8[i] = ApplyI8(s8, i8Op[i], i8C[i]); }
        for (int i = 0; i < NumI16; i++) { i16Op[i] = initOps[rng.Next(initOps.Length)]; i16C[i] = rng.Next(-32768, 32768); i16[i] = ApplyI16((short)input, i16Op[i], i16C[i]); }

        // A call-spanning signed helper with two return paths and a floor-divide inside, so the
        // signed call path is exercised under register pressure.
        int hC = rng.Next(-128, 128); int hD = Div8[rng.Next(Div8.Length)];
        int Helper(int x) => (x < 0) ? ApplyI8(x, "//", hD) : ApplyI8(ApplyI8(x, "*", 3), "-", hC);

        src.Append("from pymcu.types import int8, int16, uint8\n");
        src.Append("from pymcu.hal.uart import UART\n\n\n");
        src.Append("def hlp(x: int8) -> int8:\n");
        src.Append($"    if x < 0:\n        return x // {hD}\n    return x * 3 - {hC}\n\n\n");

        src.Append("def main():\n");
        src.Append("    uart = UART(9600)\n");
        src.Append("    uart.println(\"GO\")\n");
        src.Append("    s: uint8 = uart.read_blocking()\n");
        for (int i = 0; i < NumI8; i++) src.Append($"    a{i}: int8 = int8(s) {i8Op[i]} {i8C[i]}\n");
        for (int i = 0; i < NumI16; i++) src.Append($"    b{i}: int16 = int16(s) {i16Op[i]} {i16C[i]}\n");

        string[] ops = { "+", "-", "*", "&", "|", "^" };
        for (int stmt = 0; stmt < NumStmts; stmt++)
        {
            int choice = rng.Next(0, 12);
            if (choice < 3)                       // int8 arithmetic/bitwise
            {
                int k = rng.Next(NumI8), a = rng.Next(NumI8); string op = ops[rng.Next(ops.Length)];
                if (rng.Next(2) == 0) { int b = rng.Next(NumI8); i8[k] = ApplyI8(i8[a], op, i8[b]); src.Append($"    a{k} = a{a} {op} a{b}\n"); }
                else { int c = rng.Next(-128, 128); i8[k] = ApplyI8(i8[a], op, c); src.Append($"    a{k} = a{a} {op} {c}\n"); }
            }
            else if (choice < 5)                  // int8 floor // and %  (constant non-zero divisor)
            {
                int k = rng.Next(NumI8), a = rng.Next(NumI8), d = Div8[rng.Next(Div8.Length)];
                string op = rng.Next(2) == 0 ? "//" : "%";
                i8[k] = ApplyI8(i8[a], op, d); src.Append($"    a{k} = a{a} {op} {d}\n");
            }
            else if (choice < 7)                  // int8 shifts
            {
                int k = rng.Next(NumI8), a = rng.Next(NumI8), sh = rng.Next(0, 8);
                string op = rng.Next(2) == 0 ? "<<" : ">>";
                i8[k] = ApplyI8(i8[a], op, sh); src.Append($"    a{k} = a{a} {op} {sh}\n");
            }
            else if (choice < 9)                  // int16 arithmetic/bitwise
            {
                int k = rng.Next(NumI16), a = rng.Next(NumI16); string op = ops[rng.Next(ops.Length)];
                if (rng.Next(2) == 0) { int b = rng.Next(NumI16); i16[k] = ApplyI16(i16[a], op, i16[b]); src.Append($"    b{k} = b{a} {op} b{b}\n"); }
                else { int c = rng.Next(-32768, 32768); i16[k] = ApplyI16(i16[a], op, c); src.Append($"    b{k} = b{a} {op} {c}\n"); }
            }
            else if (choice < 11)                 // int16 floor // / % / shift
            {
                int k = rng.Next(NumI16), a = rng.Next(NumI16);
                int pick = rng.Next(3);
                if (pick == 0) { int d = Div16[rng.Next(Div16.Length)]; i16[k] = ApplyI16(i16[a], "//", d); src.Append($"    b{k} = b{a} // {d}\n"); }
                else if (pick == 1) { int d = Div16[rng.Next(Div16.Length)]; i16[k] = ApplyI16(i16[a], "%", d); src.Append($"    b{k} = b{a} % {d}\n"); }
                else { int sh = rng.Next(0, 16); string op = rng.Next(2) == 0 ? "<<" : ">>"; i16[k] = ApplyI16(i16[a], op, sh); src.Append($"    b{k} = b{a} {op} {sh}\n"); }
            }
            else                                  // call-spanning signed helper
            {
                int k = rng.Next(NumI8), a = rng.Next(NumI8);
                i8[k] = Helper(i8[a]); src.Append($"    a{k} = hlp(a{a})\n");
            }
        }

        var expected = new List<int>();
        for (int i = 0; i < NumI8; i++) { src.Append($"    print(a{i})\n"); expected.Add(i8[i]); }
        for (int i = 0; i < NumI16; i++) { src.Append($"    print(b{i})\n"); expected.Add(i16[i]); }
        src.Append("    while True:\n        pass\n");

        return new SignedStressProgram(src.ToString(), input, expected);
    }

    private static (int q, int r) FloorDivMod(int a, int b)
    {
        int q = a / b, r = a - q * b;
        if (r != 0 && (r < 0) != (b < 0)) { q -= 1; r += b; }
        return (q, r);
    }

    // PyMCU fixed-width signed semantics: operate at the operand width, wrap (two's complement).
    private static int ApplyI8(int a, string op, int b) => (sbyte)(op switch
    {
        "+" => a + b, "-" => a - b, "*" => a * b,
        "&" => a & b, "|" => a | b, "^" => a ^ b,
        "<<" => a << b, ">>" => a >> b,            // C# >> on int is arithmetic (matches ASR)
        "//" => FloorDivMod(a, b).q, "%" => FloorDivMod(a, b).r,
        _ => throw new ArgumentException(op),
    });

    private static int ApplyI16(int a, string op, int b) => (short)(op switch
    {
        "+" => a + b, "-" => a - b, "*" => a * b,
        "&" => a & b, "|" => a | b, "^" => a ^ b,
        "<<" => a << b, ">>" => a >> b,
        "//" => FloorDivMod(a, b).q, "%" => FloorDivMod(a, b).r,
        _ => throw new ArgumentException(op),
    });
}
