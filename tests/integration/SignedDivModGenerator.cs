namespace PyMCU.IntegrationTests;

/// <summary>
/// Generates a program that exercises the signed floor div/mod runtime routines
/// (__divs8/__mods8, __divs16/__mods16, __divs32/__mods32 — codegen backlog A38) over a
/// fixed list of (a, b) pairs and computes the same results with Python's floor semantics
/// as the oracle.
///
/// Operands are materialized at runtime: a seed byte of 0 is read with read_blocking() into
/// `base` (compiler-opaque), and each operand is `base + constant`. Because `base` is a runtime
/// read the division cannot constant-fold, so the actual signed routine runs. Each quotient/
/// remainder is stored into a typed local (forcing the division at the operand width, e.g. an
/// int8 // int8 stays 8-bit) and printed. The oracle wraps results to the type width so the
/// overflow edges (e.g. INT_MIN // -1) match the firmware's fixed-width wrap.
/// </summary>
public sealed class SignedDivModProgram
{
    public string Source { get; }
    public byte InputByte => 0;                 // base = int8(0) = 0
    public IReadOnlyList<long> Expected { get; }

    private SignedDivModProgram(string source, List<long> expected) => (Source, Expected) = (source, expected);

    // Python floor division/modulo, then wrapped to the signed type width (matches the
    // firmware: the quotient/remainder are stored in a fixed-width signed local).
    private static (long q, long r) FloorDivMod(long a, long b, int bytes)
    {
        long q = a / b;                          // C#: truncates toward zero
        long r = a - q * b;
        if (r != 0 && (r < 0) != (b < 0)) { q -= 1; r += b; }
        return (Wrap(q, bytes), Wrap(r, bytes));
    }

    private static long Wrap(long v, int bytes) => bytes switch
    {
        1 => (sbyte)v,
        2 => (short)v,
        _ => (int)v,
    };

    // The frontend lexer rejects the literal 2147483648 (|int32.MinValue|), so emit int32
    // MinValue as an expression whose sub-literals are each within range.
    private static string Lit(long v) => v == int.MinValue ? "(-2147483647 - 1)" : v.ToString();

    // A curated, compact set of (a, b) pairs: every sign quadrant at inexact / exact /
    // equal / |a|<|b| magnitudes, zero dividend, ±1 divisors, and the width's min/max edges
    // (including the INT_MIN // -1 overflow). Kept small so even the int32 program — where
    // each pair pulls in the large 32-bit routine — stays within the 32 KB flash of the Uno.
    private static IEnumerable<(long a, long b)> Pairs(long min, long max) => new (long, long)[]
    {
        (7, 3), (7, -3), (-7, 3), (-7, -3),     // inexact, all four sign quadrants
        (8, 2), (8, -2), (-8, 2), (-8, -2),     // exact division
        (7, 7), (-7, 7), (7, -7), (-7, -7),     // |a| == |b|
        (2, 7), (-2, 7), (2, -7), (-2, -7),     // |a| < |b|  (quotient 0 or -1)
        (0, 3), (0, -3),                        // zero dividend
        (100, 1), (-100, 1), (100, -1), (-100, -1),   // ±1 divisors
        (max, 3), (min, 3), (max, -3), (min, -3),     // magnitude edges
        (max, -1), (min, -1),                   // INT_MAX // -1, INT_MIN // -1 (overflow wrap)
        (min, min), (max, max), (min, max), (max, min),
    };

    public static SignedDivModProgram Generate(string typeName, int bytes)
    {
        long min = bytes switch { 1 => sbyte.MinValue, 2 => short.MinValue, _ => int.MinValue };
        long max = bytes switch { 1 => sbyte.MaxValue, 2 => short.MaxValue, _ => int.MaxValue };

        var pairs = Pairs(min, max).ToList();
        var expected = new List<long>();
        var src = new System.Text.StringBuilder();

        src.Append($"from pymcu.types import {typeName}, uint8\n");
        src.Append("from pymcu.hal.uart import UART\n\n\n");
        src.Append("def main():\n");
        src.Append("    uart = UART(9600)\n");
        src.Append("    uart.println(\"GO\")\n");
        src.Append("    s: uint8 = uart.read_blocking()\n");
        src.Append($"    base: {typeName} = {typeName}(s)\n");   // = 0 at runtime, opaque to the compiler

        int n = 0;
        foreach (var (a, b) in pairs)
        {
            // base + a / base + b -> a / b at runtime (base == 0), but not constant-folded.
            src.Append($"    a{n}: {typeName} = base + {Lit(a)}\n");
            src.Append($"    b{n}: {typeName} = base + {Lit(b)}\n");
            src.Append($"    q{n}: {typeName} = a{n} // b{n}\n");
            src.Append($"    r{n}: {typeName} = a{n} % b{n}\n");
            // Print each result as its raw little-endian bytes rather than the value, so the
            // check validates the exact 32-bit pattern independent of the signed-print path
            // (print(int32) still truncates to 16 bits — A37). Each byte is 0..255, which the
            // unsigned formatter prints verbatim. Shift amounts (0,8,16,24) stay below the width.
            var (q, r) = FloorDivMod(a, b, bytes);
            for (int k = 0; k < bytes; k++)
            {
                // uint8(...) reinterprets the selected byte as unsigned (0..255), avoiding
                // any ambiguity about the masked expression's type.
                src.Append($"    print(uint8(q{n} >> {8 * k}))\n");
                expected.Add((q >> (8 * k)) & 0xFF);
            }
            for (int k = 0; k < bytes; k++)
            {
                src.Append($"    print(uint8(r{n} >> {8 * k}))\n");
                expected.Add((r >> (8 * k)) & 0xFF);
            }
            n++;
        }

        src.Append("    while True:\n        pass\n");
        return new SignedDivModProgram(src.ToString(), expected);
    }
}
