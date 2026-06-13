namespace PyMCU.IntegrationTests;

/// <summary>
/// Interrupt-safety across the Z pointer (codegen backlog A36). main does runtime-indexed array
/// reads in a loop (which materialize the address into Z = R30:R31) while an ISR that ALSO does
/// a runtime-indexed array access fires. The ISR clobbers Z; the context-save must preserve it,
/// or main's in-progress array address is corrupted. This is the array/flash counterpart to the
/// uint16-MUL register-pair coverage — both exercise the caller-clobbered registers (R30/R31
/// here) that the old fixed ISR save set omitted. main's printed values must equal the
/// ISR-free reference.
/// </summary>
public sealed class IsrArrayProgram
{
    public string Source { get; }
    public byte InputByte { get; }
    public IReadOnlyList<int> Expected { get; }

    private IsrArrayProgram(string source, byte input, List<int> expected)
        => (Source, InputByte, Expected) = (source, input, expected);

    private const int ArrLen = 8;
    private const int Reps = 40;

    public static IsrArrayProgram Generate(int seed)
    {
        var rng = new Random(seed);
        byte input = (byte)(seed * 97 + 31);
        var src = new System.Text.StringBuilder();

        int s = input;
        var arr = new int[ArrLen];
        for (int i = 0; i < ArrLen; i++) arr[i] = rng.Next(0, 256);
        // ISR's own table (independent of main).
        var gtbl = new int[4];
        for (int i = 0; i < 4; i++) gtbl[i] = rng.Next(0, 256);
        int step = (rng.Next(0, 4) * 2) + 1;   // odd → cycles through all 8 indices

        int acc = s, idx = 0, x = (s ^ 0x5A) & 0xFF;

        src.Append("from pymcu.types import uint8, interrupt, asm\n");
        src.Append("from pymcu.chips.atmega328p import TCCR2B, TIMSK2\n");
        src.Append("from pymcu.hal.uart import UART\n\n\n");
        src.Append($"gtbl: uint8[4] = [{gtbl[0]}, {gtbl[1]}, {gtbl[2]}, {gtbl[3]}]\n");
        src.Append("gidx: uint8 = 0\n");
        src.Append("gcnt: uint8 = 0\n\n\n");
        // ISR does a runtime-indexed table read → uses Z (R30:R31).
        src.Append("@interrupt(0x0012)\n");
        src.Append("def t2_ovf():\n");
        src.Append("    global gidx, gcnt\n");
        src.Append("    gcnt = gtbl[gidx]\n");
        src.Append("    gidx = (gidx + 1) & 3\n\n\n");

        src.Append("def main():\n");
        src.Append("    uart = UART(9600)\n");
        src.Append("    uart.println(\"GO\")\n");
        src.Append("    TCCR2B.value = 0x03\n    TIMSK2.value = 0x01\n");
        src.Append("    asm(\"SEI\")\n");
        src.Append("    s: uint8 = uart.read_blocking()\n");
        src.Append("    arr: uint8[8] = [" + string.Join(", ", arr) + "]\n");
        src.Append("    acc: uint8 = s\n");
        src.Append("    idx: uint8 = 0\n");
        src.Append("    x: uint8 = s ^ 90\n");
        src.Append("    r: uint8 = 0\n");
        src.Append($"    while r < {Reps}:\n");
        src.Append("        acc = acc + arr[idx]\n");          // runtime-indexed read → Z
        src.Append($"        idx = (idx + {step}) & 7\n");
        src.Append("        x = x * 3\n");
        src.Append("        acc = acc ^ x\n");
        src.Append("        r = r + 1\n");

        for (int rep = 0; rep < Reps; rep++)
        {
            acc = (acc + arr[idx]) & 0xFF;
            idx = (idx + step) & 7;
            x = (x * 3) & 0xFF;
            acc = (acc ^ x) & 0xFF;
        }

        var expected = new List<int> { acc, idx, x };
        src.Append("    print(acc)\n    print(idx)\n    print(x)\n");
        src.Append("    while True:\n        pass\n");

        return new IsrArrayProgram(src.ToString(), input, expected);
    }
}
