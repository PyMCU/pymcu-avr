using FluentAssertions;
using NUnit.Framework;
using Avr8Sharp.TestKit.Boards;

namespace PyMCU.IntegrationTests.Tests.AVR;

/// <summary>
/// Bug-hunting probes for Python language fidelity. Each builds a tiny program, seeds a runtime
/// value over UART (so nothing constant-folds), and checks the printed lines against the Python
/// oracle. A failure here is a silent miscompilation, not a crash.
/// </summary>
[TestFixture]
public class FidelityProbeTests
{
    private static int NL(string s) { int n = 0; foreach (var c in s) if (c == '\n') n++; return n; }

    private static List<long> RunSeed(string body, int seed, int expectedLines)
    {
        string src =
            "from pymcu.types import uint8\n" +
            "from pymcu.hal.uart import UART\n\n\n" +
            body + "\n" +
            "def main():\n" +
            "    uart = UART(9600)\n" +
            "    uart.println(\"GO\")\n" +
            "    s: uint8 = uart.read_blocking()\n" +
            "    run(s)\n" +
            "    while True:\n        pass\n";

        var hex = PymcuCompiler.BuildSource(src);
        var uno = new ArduinoUnoSimulation();
        uno.WithHex(hex);
        uno.RunUntilSerial(uno.Serial, "GO\n", maxMs: 500);
        uno.Serial.InjectByte((byte)seed);
        uno.RunUntilSerial(uno.Serial, t => NL(t) >= expectedLines + 1, maxMs: 6000);

        var lines = uno.Serial.Text.Replace("\r", "").Split('\n');
        int start = Array.FindIndex(lines, l => l.Trim() == "GO");
        var got = new List<long>();
        for (int i = start + 1; i < lines.Length && got.Count < expectedLines; i++)
            if (long.TryParse(lines[i].Trim(), out long v)) got.Add(v);
        return got;
    }

    [Test]
    public void ChainedComparison()
    {
        // 1 < s < 10 ; 1 < s < 4 ; 0 < s <= 5   with s=5  ->  1, 0, 1
        const string body =
            "def run(s: uint8):\n" +
            "    print(1 if 1 < s < 10 else 0)\n" +
            "    print(1 if 1 < s < 4 else 0)\n" +
            "    print(1 if 0 < s <= 5 else 0)\n";
        RunSeed(body, 5, 3).Should().Equal(1, 0, 1);
    }

    [Test]
    public void ShortCircuit_Or_SkipsRhs()
    {
        // bump() returns 1 and increments a module counter. `True or bump()` must NOT call bump.
        // `False or bump()` must call it. Print the counter after each.
        const string body =
            "hits: uint8 = 0\n\n" +
            "def bump() -> uint8:\n" +
            "    global hits\n" +
            "    hits = hits + 1\n" +
            "    return 1\n\n" +
            "def run(s: uint8):\n" +
            "    global hits\n" +
            "    a: bool = (s > 0) or (bump() > 0)\n" +   // s>0 True -> bump skipped
            "    print(hits)\n" +                          // 0
            "    b: bool = (s > 100) and (bump() > 0)\n" + // s>100 False -> bump skipped
            "    print(hits)\n" +                          // 0
            "    c: bool = (s > 100) or (bump() > 0)\n" +  // False or -> bump runs
            "    print(hits)\n";                           // 1
        RunSeed(body, 5, 3).Should().Equal(0, 0, 1);
    }

    [Test]
    public void MultipleAssignment_EvalsOnce()
    {
        // a = b = s + 1  -> both a and b equal s+1, expr evaluated once.
        const string body =
            "def run(s: uint8):\n" +
            "    a: uint8 = 0\n" +
            "    b: uint8 = 0\n" +
            "    a = b = s + 1\n" +
            "    print(a)\n" +
            "    print(b)\n";
        RunSeed(body, 5, 2).Should().Equal(6, 6);
    }

    [Test]
    public void AugAssign_OnArrayElement()
    {
        // arr[i] += s for a runtime-ish constant index, verify it reads then writes.
        const string body =
            "def run(s: uint8):\n" +
            "    arr: uint8[3] = [10, 20, 30]\n" +
            "    arr[1] += s\n" +     // 20 + 5 = 25
            "    arr[2] -= s\n" +     // 30 - 5 = 25
            "    print(arr[0])\n" +   // 10
            "    print(arr[1])\n" +   // 25
            "    print(arr[2])\n";    // 25
        RunSeed(body, 5, 3).Should().Equal(10, 25, 25);
    }

    [Test]
    public void DefaultAndKeywordArgs()
    {
        const string body =
            "def addk(a: uint8, b: uint8 = 10, c: uint8 = 100) -> uint8:\n" +
            "    return a + b + c\n\n" +
            "def run(s: uint8):\n" +
            "    print(addk(s))\n" +        // 5+10+100 = 115
            "    print(addk(s, c=1))\n" +   // 5+10+1  = 16
            "    print(addk(s, 2))\n";      // 5+2+100 = 107
        RunSeed(body, 5, 3).Should().Equal(115, 16, 107);
    }

    [Test]
    public void WhileBreakContinue()
    {
        // sum odd i in 1..s, break once i exceeds s.  s=5 -> 1+3+5 = 9
        const string body =
            "def run(s: uint8):\n" +
            "    total: uint8 = 0\n" +
            "    i: uint8 = 0\n" +
            "    while True:\n" +
            "        i += 1\n" +
            "        if i > s:\n" +
            "            break\n" +
            "        if i % 2 == 0:\n" +
            "            continue\n" +
            "        total += i\n" +
            "    print(total)\n";
        RunSeed(body, 5, 1).Should().Equal(9);
    }

    [Test]
    public void SignedArithmeticShift()
    {
        // int8(-8) >> 1 = -4 (arithmetic shift keeps sign); seed only gates execution.
        const string body =
            "from pymcu.types import int8\n\n" +
            "def run(s: uint8):\n" +
            "    x: int8 = int8(0) - int8(s) - 3\n" +   // -(5)-3 = -8
            "    y: int8 = x >> 1\n" +
            "    print(y)\n";                            // -4
        RunSeed(body, 5, 1).Should().Equal(-4);
    }

    [Test]
    public void MixedWidthMultiply_WidensToTarget()
    {
        // uint8 * uint8 assigned to uint16 must compute the full 16-bit product (300), not
        // wrap at 8 bits (44). This is the classic AOT fixed-width promotion trap.
        const string body =
            "from pymcu.types import uint16\n\n" +
            "def run(s: uint8):\n" +
            "    a: uint8 = 60\n" +
            "    r: uint16 = a * s\n" +   // 60*5 = 300
            "    print(r)\n";
        RunSeed(body, 5, 1).Should().Equal(300);
    }

    [Test]
    public void Uint8Wraparound()
    {
        // Documented fixed-width wrap: 250+5=255, 255+5 = 260 & 0xFF = 4.
        const string body =
            "def run(s: uint8):\n" +
            "    w: uint8 = 250 + s\n" +
            "    print(w)\n" +            // 255
            "    w = w + s\n" +
            "    print(w)\n";             // 4
        RunSeed(body, 5, 2).Should().Equal(255, 4);
    }

    [Test]
    public void BoolArithmetic()
    {
        // Comparisons are 0/1 integers in arithmetic: (s>0)+(s>3)+(s>100) = 1+1+0 = 2.
        const string body =
            "def run(s: uint8):\n" +
            "    cnt: uint8 = (s > 0) + (s > 3) + (s > 100)\n" +
            "    print(cnt)\n";
        RunSeed(body, 5, 1).Should().Equal(2);
    }

    [Test]
    public void TupleUnpackFromInlineReturn()
    {
        const string body =
            "@inline\n" +
            "def divmod2(a: uint8, b: uint8) -> tuple[uint8, uint8]:\n" +
            "    return a // b, a % b\n\n" +
            "def run(s: uint8):\n" +
            "    q: uint8 = 0\n" +
            "    rem: uint8 = 0\n" +
            "    q, rem = divmod2(s * 5, 7)\n" +   // 25//7=3, 25%7=4
            "    print(q)\n" +
            "    print(rem)\n";
        RunSeed(body, 5, 2).Should().Equal(3, 4);
    }

    [Test]
    public void NestedLoopInnerBreak()
    {
        const string body =
            "def run(s: uint8):\n" +
            "    acc: uint8 = 0\n" +
            "    for i in range(3):\n" +
            "        for j in range(10):\n" +
            "            if j >= s:\n" +
            "                break\n" +
            "            acc += 1\n" +
            "    print(acc)\n";    // 3 * min(10, s) = 15
        RunSeed(body, 5, 1).Should().Equal(15);
    }

    [Test]
    public void RuntimeBoundRange()
    {
        const string body =
            "def run(s: uint8):\n" +
            "    acc: uint8 = 0\n" +
            "    for i in range(s):\n" +
            "        acc += i\n" +
            "    print(acc)\n" +           // 0+1+2+3+4 = 10
            "    c: uint8 = 0\n" +
            "    for i in range(s):\n" +
            "        if i * i > s:\n" +
            "            break\n" +
            "        c += 1\n" +
            "    print(c)\n";              // i=0,1,2 ok; 3*3=9>5 break -> 3
        RunSeed(body, 5, 2).Should().Equal(10, 3);
    }

    [Test]
    public void RuntimeArrayIndex()
    {
        const string body =
            "def run(s: uint8):\n" +
            "    arr: uint8[5] = [0, 0, 0, 0, 0]\n" +
            "    idx: uint8 = s - 3\n" +   // 2
            "    arr[idx] = 42\n" +
            "    print(arr[2])\n" +        // 42
            "    arr[idx] += 8\n" +
            "    print(arr[idx])\n";       // 50
        RunSeed(body, 5, 2).Should().Equal(42, 50);
    }

    [Test]
    public void SignedFloorDivMod_Negatives()
    {
        // Python floor semantics: -7//2 = -4, -7%2 = 1, -7%3 = 2.
        const string body =
            "from pymcu.types import int8\n\n" +
            "def run(s: uint8):\n" +
            "    a: int8 = int8(0) - int8(s) - 2\n" +   // -7
            "    print(a // 2)\n" +
            "    print(a % 2)\n" +
            "    print(a % 3)\n";
        RunSeed(body, 5, 3).Should().Equal(-4, 1, 2);
    }

    [Test]
    public void BitwiseNot_Uint8()
    {
        const string body =
            "def run(s: uint8):\n" +
            "    n: uint8 = s\n" +
            "    print(uint8(~n))\n";   // ~5 = 250 in 8-bit
        RunSeed(body, 5, 1).Should().Equal(250);
    }

    [Test]
    public void VariableShiftAmount()
    {
        const string body =
            "from pymcu.types import uint16\n\n" +
            "def run(s: uint8):\n" +
            "    print(uint16(1) << s)\n" +        // 32
            "    print(s << 1)\n" +                // 10
            "    print(uint8(128) >> (s - 3))\n";  // 128>>2 = 32
        RunSeed(body, 5, 3).Should().Equal(32, 10, 32);
    }

    [Test]
    public void Uint16RuntimeLoopAccumulation()
    {
        const string body =
            "from pymcu.types import uint16\n\n" +
            "def run(s: uint8):\n" +
            "    tot: uint16 = 0\n" +
            "    for i in range(s):\n" +
            "        tot += 100\n" +
            "    print(tot)\n";   // 5*100 = 500 (overflows 8-bit)
        RunSeed(body, 5, 1).Should().Equal(500);
    }

    [Test]
    public void MixedSignednessComparison()
    {
        // int8(-1) < int8(5) is True (Python: -1 < 5). Guards against unsigned reinterpretation.
        const string body =
            "from pymcu.types import int8\n\n" +
            "def run(s: uint8):\n" +
            "    neg: int8 = int8(0) - 1\n" +
            "    print(1 if neg < int8(s) else 0)\n";   // 1
        RunSeed(body, 5, 1).Should().Equal(1);
    }

    [Test]
    public void BytesLiteralIndexAndLen()
    {
        const string body =
            "def run(s: uint8):\n" +
            "    data = b\"ABCDE\"\n" +
            "    print(data[0])\n" +    // 'A' = 65
            "    print(len(data))\n";   // 5
        RunSeed(body, 5, 2).Should().Equal(65, 5);
    }

    [Test]
    public void GlobalMutationAcrossCalls()
    {
        // Regression guard for the PropagateCopies Call-dst fix: a global bumped inside a callee
        // must read its accumulated value after the call, not the pre-call constant.
        const string body =
            "from pymcu.types import uint16\n\n" +
            "counter: uint16 = 0\n\n" +
            "def tick(n: uint8):\n" +
            "    global counter\n" +
            "    counter = counter + n\n\n" +
            "def run(s: uint8):\n" +
            "    tick(s)\n" +
            "    tick(s)\n" +
            "    print(counter)\n";   // 10
        RunSeed(body, 5, 1).Should().Equal(10);
    }

    [Test]
    public void Uint32FullArithmetic()
    {
        const string body =
            "from pymcu.types import uint32\n\n" +
            "def run(s: uint8):\n" +
            "    base: uint32 = uint32(s) * 1000000\n" +
            "    print(base + 12345)\n" +
            "    print(base - 6999999)\n" +
            "    print(base // 7)\n" +
            "    print(base % 999)\n" +
            "    print(base >> 4)\n" +
            "    print(base & 0xFFFF)\n";
        const long b = 7 * 1000000L;
        RunSeed(body, 7, 6).Should().Equal(
            b + 12345, b - 6999999, b / 7, b % 999, b >> 4, b & 0xFFFF);
    }

    [Test]
    public void BoolOrShortCircuit_AsCondition()
    {
        // Regression: `A or B` used directly as an if/ternary condition must short-circuit to
        // TRUE when A is true, not fall through to evaluate B. CollapseBoolJumps used to fuse B's
        // comparison into the jump past the OR's end label, dropping the A-true path. All six
        // forms (with/without parens, either operand order, via a bool var) must be True for s=7.
        const string body =
            "def run(s: uint8):\n" +
            "    print(1 if (s > 5 and s < 10) else 0)\n" +
            "    print(1 if (s > 5 and s < 10) or s == 0 else 0)\n" +
            "    print(1 if s == 0 or (s > 5 and s < 10) else 0)\n" +
            "    print(1 if (s > 5) or s == 0 else 0)\n" +
            "    b: bool = (s > 5 and s < 10) or s == 0\n" +
            "    print(1 if b else 0)\n" +
            "    print(1 if s > 5 and s < 10 or s == 0 else 0)\n";
        RunSeed(body, 7, 6).Should().Equal(1, 1, 1, 1, 1, 1);
    }

    [Test]
    public void BoolCompound_AndOrNot()
    {
        const string body =
            "def run(s: uint8):\n" +
            "    print(1 if s < 3 and s < 10 else 0)\n" +                       // 0
            "    print(1 if not (s < 3 or s > 5) else 0)\n" +                   // 0
            "    print(1 if (s > 5 or s < 1) and (s < 10 or s == 0) else 0)\n" + // 1
            "    print(1 if s > 100 or s > 50 or s > 5 else 0)\n" +             // 1
            "    print(1 if s < 1 or s < 2 or s < 3 else 0)\n" +                // 0
            "    print(1 if (s > 10 and s < 20) or s == 7 else 0)\n" +          // 1
            "    print(1 if s > 10 or s < 5 and s > 0 else 0)\n" +              // 0
            "    print(1 if not s == 7 else 0)\n" +                            // 0
            "    i: uint8 = 0\n" +
            "    while i < s and i < 3:\n" +
            "        i += 1\n" +
            "    print(i)\n";                                                   // 3
        RunSeed(body, 7, 9).Should().Equal(0, 0, 1, 1, 0, 1, 0, 0, 3);
    }

    [Test]
    public void AndOr_ReturnOperand_PythonSemantics()
    {
        // Python: `a or b`/`a and b` evaluate to an OPERAND, not a coerced bool.
        const string body =
            "def run(s: uint8):\n" +
            "    print(s or 100)\n" +      // s truthy -> 7
            "    print(0 or s)\n" +        // 0 falsy  -> 7
            "    print(s and 100)\n" +     // s truthy -> 100
            "    print(0 and s)\n" +       // 0 falsy  -> 0
            "    a: uint8 = 0\n" +
            "    print(a or s or 200)\n" + // 0 or 7 -> 7
            "    print(a and 5 or s)\n";   // (0 and 5)=0, 0 or 7 -> 7
        RunSeed(body, 7, 6).Should().Equal(7, 7, 100, 0, 7, 7);
    }

    [Test]
    public void IfStatement_CompoundConditions()
    {
        const string body =
            "def run(s: uint8):\n" +
            "    r: uint8 = 9\n" +
            "    if (s > 5) or (s == 0):\n        r = 1\n    else:\n        r = 2\n" +
            "    print(r)\n" +     // 1
            "    if (s > 10) or (s == 0):\n        r = 3\n    else:\n        r = 4\n" +
            "    print(r)\n" +     // 4
            "    if (s > 5) and (s < 10):\n        r = 5\n    else:\n        r = 6\n" +
            "    print(r)\n" +     // 5
            "    if (s > 10) and (s < 20):\n        r = 7\n    else:\n        r = 8\n" +
            "    print(r)\n" +     // 8
            "    if s < 3:\n        r = 10\n    elif s > 5 or s == 4:\n        r = 11\n    else:\n        r = 12\n" +
            "    print(r)\n";      // 11
        RunSeed(body, 7, 5).Should().Equal(1, 4, 5, 8, 11);
    }

    [Test]
    public void PrintSignedCastDirect()
    {
        // Regression: print(int8(x)) must format signed. CoalesceInstructions used to retarget
        // `copy u8 -> i8; copy i8 -> i16` into `copy u8 -> i16`, zero-extending the value so a
        // direct print of a signed cast showed the unsigned byte (200 instead of -56).
        const string body =
            "from pymcu.types import int8\n\n" +
            "def run(s: uint8):\n" +
            "    print(int8(s))\n" +     // -56  (direct cast in print)
            "    x: int8 = int8(s)\n" +
            "    print(x)\n" +           // -56  (via typed var)
            "    y: int8 = int8(s)\n" +
            "    print(y + 0)\n" +       // -56  (arithmetic)
            "    print(int8(200))\n";    // -56  (constant cast)
        RunSeed(body, 200, 4).Should().Equal(-56, -56, -56, -56);
    }

    [Test]
    public void WidthCasts_TruncateSignZeroExtend()
    {
        const string body =
            "from pymcu.types import int8, uint16, int16, uint32\n\n" +
            "def run(s: uint8):\n" +
            "    big: uint16 = uint16(s) + 300\n" +
            "    print(uint8(big))\n" +        // 500 -> 244 (truncate)
            "    print(int8(s))\n" +           // 200 -> -56 (reinterpret)
            "    neg: int8 = int8(0) - 1\n" +
            "    print(uint16(neg))\n" +       // -1 -> 65535 (sign-extend)
            "    n2: int8 = int8(s)\n" +
            "    print(int16(n2))\n" +         // -56 -> -56 (sign-extend)
            "    print(int16(s))\n" +          // 200 -> 200 (zero-extend)
            "    print(uint32(s) * 100000)\n"; // 200*100000 = 20_000_000
        RunSeed(body, 200, 6).Should().Equal(244, -56, 65535, -56, 200, 20000000);
    }

    [Test]
    public void FloatArithmeticAndConversions()
    {
        const string src =
            "from pymcu.types import uint8, int16\n" +
            "from pymcu.hal.uart import UART\n\n\n" +
            "def main():\n" +
            "    uart = UART(9600)\n" +
            "    uart.println(\"GO\")\n" +
            "    s: uint8 = uart.read_blocking()\n" +
            "    f: float = float(s) / 4.0\n" +
            "    print(f)\n" +              // 2.5
            "    g: float = f * 2.0\n" +
            "    print(g)\n" +              // 5.0
            "    print(int16(f * 100.0))\n" + // 250
            "    h: float = float(s) + 0.5\n" +
            "    print(int16(h))\n" +       // 10
            "    n: int16 = int16(0) - int16(s) * 30\n" +
            "    print(n)\n" +              // -300
            "    print(uint8(int16(s) * 30))\n" + // 44
            "    print(1 if f < g else 0)\n" +    // 1
            "    while True:\n        pass\n";

        var hex = PymcuCompiler.BuildSource(src);
        var uno = new ArduinoUnoSimulation();
        uno.WithHex(hex);
        uno.RunUntilSerial(uno.Serial, "GO\n", maxMs: 500);
        uno.Serial.InjectByte(10);
        uno.RunUntilSerial(uno.Serial, t => NL(t) >= 8, maxMs: 6000);

        var lines = uno.Serial.Text.Replace("\r", "").Split('\n');
        int start = Array.FindIndex(lines, l => l.Trim() == "GO");
        var got = new List<string>();
        for (int i = start + 1; i < lines.Length && got.Count < 7; i++)
            if (lines[i].Trim().Length > 0) got.Add(lines[i].Trim());

        got[0].Should().StartWith("2.5");
        got[1].Should().StartWith("5.0");
        got[2].Should().Be("250");
        got[3].Should().Be("10");
        got[4].Should().Be("-300");
        got[5].Should().Be("44");
        got[6].Should().Be("1");
    }

    [Test]
    public void Int8SramArray_SignExtendsOnLoad()
    {
        // Regression: a runtime index forces an int8 array into SRAM. Loading an element for a
        // wider use (print -> i16) must sign-extend. CoalesceInstructions used to retarget the
        // ArrayLoad's int8 dst onto the int16 temp, leaving the high byte unset (-5 read as 251).
        const string body =
            "from pymcu.types import int8\n\n" +
            "def run(s: uint8):\n" +
            "    arr: int8[4] = [0, 0, 0, 0]\n" +
            "    arr[0] = int8(0) - int8(s)\n" +
            "    arr[1] = int8(s)\n" +
            "    idx: uint8 = s - 4\n" +
            "    print(arr[idx])\n" +   // arr[1] = 5
            "    print(arr[0])\n" +     // -5
            "    j: uint8 = s - 5\n" +
            "    print(arr[j])\n";      // arr[0] = -5
        RunSeed(body, 5, 3).Should().Equal(5, -5, -5);
    }

    [Test]
    public void SignedThroughArraysAndFunctions()
    {
        const string body =
            "from pymcu.types import int8, int16\n\n" +
            "def neg_of(x: int8) -> int8:\n" +
            "    return int8(0) - x\n\n" +
            "def run(s: uint8):\n" +
            "    a: int8 = int8(0) - int8(s)\n" +   // -5
            "    print(neg_of(a))\n" +              // 5
            "    print(neg_of(int8(s)))\n" +        // -5
            "    arr: int8[4] = [0, 0, 0, 0]\n" +
            "    arr[0] = int8(0) - int8(s)\n" +    // -5
            "    arr[1] = int8(s)\n" +              // 5
            "    idx: uint8 = s - 4\n" +            // 1
            "    arr[2] = arr[idx]\n" +             // 5
            "    print(arr[0])\n" +                 // -5
            "    print(arr[2])\n" +                 // 5
            "    total: int16 = 0\n" +
            "    for i in range(4):\n" +
            "        total += int16(arr[i])\n" +
            "    print(total)\n";                   // -5+5+5+0 = 5
        RunSeed(body, 5, 5).Should().Equal(5, -5, -5, 5, 5);
    }

    [Test]
    public void AnnotatedDeclRuntimeArrayIndex()
    {
        // Regression: `v: T = arr[idx]` (a typed declaration whose initializer reads a
        // runtime-indexed array) used to error "subscript must be compile-time constant",
        // while `v: T = 0; v = arr[idx]` worked. ScanForVariableIndexedArrays now scans
        // VarDecl initializers (and for-loop bodies), so arr is marked SRAM-indexed.
        const string body =
            "def run(s: uint8):\n" +
            "    arr: uint8[4] = [10, 20, 30, 40]\n" +
            "    idx: uint8 = s - 4\n" +    // 1
            "    v: uint8 = arr[idx]\n" +
            "    print(v)\n";              // arr[1] = 20
        RunSeed(body, 5, 1).Should().Equal(20);
    }

    [Test]
    public void InlineComputedArrayIndex_InExpression()
    {
        // Regression (AvrLinearScan): the live interval of a temp defined by an ArrayLoad was
        // invisible (the array ops were missing from the liveness walk), so an earlier load's
        // result shared R16 with a later load's inline index and got clobbered.
        // `arr[idx] + arr[s - 5]` returned just the second element.
        const string body =
            "def run(s: uint8):\n" +
            "    arr: uint8[5] = [10, 20, 30, 40, 50]\n" +
            "    idx: uint8 = s - 4\n" +
            "    print(arr[idx] + arr[s - 5])\n" +  // 20+10 = 30
            "    print(arr[s - 5] + arr[idx])\n" +  // 10+20 = 30
            "    print(arr[s - 4])\n";              // 20
        RunSeed(body, 5, 3).Should().Equal(30, 30, 20);
    }

    [Test]
    public void BytearrayAndUint16_TwoLoadsInExpression()
    {
        const string body =
            "from pymcu.types import uint16\n\n" +
            "def sum2(buf: bytearray, i: uint8) -> uint8:\n" +
            "    return buf[i] + buf[i + 1]\n\n" +
            "def run(s: uint8):\n" +
            "    data: uint8[4] = [10, 20, 30, 40]\n" +
            "    print(sum2(data, s))\n" +        // buf[1]+buf[2] = 50
            "    w: uint16[3] = [100, 200, 300]\n" +
            "    k: uint16 = w[s] + w[s - 1]\n" +
            "    print(k)\n";                      // w[1]+w[0] = 300
        RunSeed(body, 1, 2).Should().Equal(50, 300);
    }

    [Test]
    public void TwoRuntimeArrayLoads_StoredIndices()
    {
        // Two SRAM array loads with pre-stored runtime indices combine correctly.
        const string body =
            "def run(s: uint8):\n" +
            "    arr: uint8[5] = [10, 20, 30, 40, 50]\n" +
            "    i: uint8 = s - 4\n" +    // 1
            "    j: uint8 = s - 5\n" +    // 0
            "    print(arr[i] + arr[j])\n" + // 20+10 = 30
            "    k: uint8 = s - 3\n" +    // 2
            "    print(arr[i] + arr[k])\n"; // 20+30 = 50
        RunSeed(body, 5, 2).Should().Equal(30, 50);
    }

    [Test]
    public void TwoIndirectCallsInExpression()
    {
        const string body =
            "from pymcu.types import Callable\n\n" +
            "def add_one(x: uint8) -> uint8:\n" +
            "    return x + 1\n\n" +
            "def add_two(x: uint8) -> uint8:\n" +
            "    return x + 2\n\n" +
            "def run(s: uint8):\n" +
            "    fn: Callable = add_one\n" +
            "    fn2: Callable = add_two\n" +
            "    a: uint8 = fn(s) + fn2(s)\n" +   // 4 + 5 = 9
            "    print(a)\n";
        var got = RunSeed(body, 3, 1);
        TestContext.WriteLine("GOT: " + string.Join(",", got));
        got.Should().Equal(9);
    }

    [Test]
    public void MultiFieldZca_MutateTwoInstances()
    {
        const string body =
            "from pymcu.types import uint16\n\n" +
            "class Point:\n" +
            "    def __init__(self, x: uint8, y: uint8):\n" +
            "        self.x = x\n" +
            "        self.y = y\n\n" +
            "    def move(self, dx: uint8, dy: uint8):\n" +
            "        self.x = self.x + dx\n" +
            "        self.y = self.y + dy\n\n" +
            "    def total(self) -> uint16:\n" +
            "        return uint16(self.x) + uint16(self.y)\n\n" +
            "def run(s: uint8):\n" +
            "    p = Point(s, s + 1)\n" +
            "    q = Point(s + 2, s + 3)\n" +
            "    p.move(10, 20)\n" +
            "    q.move(s, s)\n" +
            "    print(p.total())\n" +   // 15+26 = 41
            "    print(q.total())\n" +   // 12+13 = 25
            "    print(p.x)\n" +         // 15
            "    print(q.y)\n";          // 13
        var got = RunSeed(body, 5, 4);
        TestContext.WriteLine("GOT: " + string.Join(",", got));
        got.Should().Equal(41, 25, 15, 13);
    }

    [Test]
    public void MultiFieldZca_WideField()
    {
        // A slot (multi-field) ZCA with a uint16 field must store/load all bytes, not just the
        // low one. Previously total=1500 read back as 220 (1500 & 0xFF). Mixed with a uint8 field
        // to exercise non-trivial byte offsets, via a method and direct access.
        const string body =
            "from pymcu.types import uint16\n\n" +
            "class Acc:\n" +
            "    def __init__(self, base: uint16, tag: uint8):\n" +
            "        self.total = base\n" +
            "        self.tag = tag\n\n" +
            "    def add(self, v: uint16):\n" +
            "        self.total = self.total + v\n\n" +
            "    def get(self) -> uint16:\n" +
            "        return self.total\n\n" +
            "def run(s: uint8):\n" +
            "    a = Acc(uint16(s) * 100, s)\n" +
            "    a.add(500)\n" +
            "    print(a.get())\n" +   // 1000+500 = 1500
            "    print(a.total)\n" +   // 1500 (direct)
            "    print(a.tag)\n";      // 10 (uint8 field after a uint16)
        RunSeed(body, 10, 3).Should().Equal(1500, 1500, 10);
    }

    [Test]
    public void EnumArithmeticAndCompare()
    {
        const string body =
            "from pymcu.types import Enum\n\n" +
            "class Color(Enum):\n" +
            "    RED = 1\n" +
            "    GREEN = 2\n" +
            "    BLUE = 4\n\n" +
            "def run(s: uint8):\n" +
            "    print(Color.RED + Color.BLUE)\n" +           // 5
            "    print(1 if s == Color.GREEN else 0)\n" +     // 1 (s=2)
            "    print(Color.RED | Color.GREEN | Color.BLUE)\n" + // 7
            "    print(s + Color.RED)\n";                     // 3
        RunSeed(body, 2, 4).Should().Equal(5, 1, 7, 3);
    }

    [Test]
    public void ListAppendIndexIterate()
    {
        const string body =
            "def run(s: uint8):\n" +
            "    lst: list[uint8] = []\n" +
            "    lst.append(s)\n" +
            "    lst.append(s + 1)\n" +
            "    lst.append(s + 2)\n" +
            "    print(len(lst))\n" +   // 3
            "    print(lst[0])\n" +     // 5
            "    print(lst[2])\n" +     // 7
            "    lst[1] = 99\n" +
            "    print(lst[1])\n" +     // 99
            "    total: uint8 = 0\n" +
            "    for v in lst:\n" +
            "        total += v\n" +
            "    print(total)\n";       // 5+99+7 = 111
        var got = RunSeed(body, 5, 5);
        TestContext.WriteLine("GOT=" + string.Join(",", got));
        got.Should().Equal(3, 5, 7, 99, 111);
    }

    [Test]
    public void NegativeArrayIndex()
    {
        const string body =
            "def run(s: uint8):\n" +
            "    arr: uint8[4] = [10, 20, 30, 40]\n" +
            "    print(arr[-1])\n" +    // 40
            "    print(arr[-2])\n";     // 30
        RunSeed(body, 0, 2).Should().Equal(40, 30);
    }

    [Test]
    public void SliceInForLoop()
    {
        const string body =
            "def run(s: uint8):\n" +
            "    arr: uint8[5] = [10, 20, 30, 40, 50]\n" +
            "    total: uint8 = 0\n" +
            "    for v in arr[1:4]:\n" +
            "        total += v\n" +
            "    print(total)\n";       // 20+30+40 = 90
        RunSeed(body, 0, 1).Should().Equal(90);
    }

    [Test]
    public void ListUint16Elements()
    {
        const string body =
            "from pymcu.types import uint16\n\n" +
            "def run(s: uint8):\n" +
            "    lst: list[uint16] = []\n" +
            "    lst.append(uint16(s) * 1000)\n" +   // 3000
            "    lst.append(uint16(s) * 100)\n" +    // 300
            "    print(lst[0])\n" +                   // 3000
            "    print(lst[1])\n" +                   // 300
            "    lst[0] = 50000\n" +
            "    print(lst[0])\n" +                   // 50000
            "    tot: uint16 = 0\n" +
            "    for v in lst:\n" +
            "        tot += v\n" +
            "    print(tot)\n";                       // 50000+300 = 50300
        var got = RunSeed(body, 3, 4);
        TestContext.WriteLine("GOT=" + string.Join(",", got));
        got.Should().Equal(3000, 300, 50000, 50300);
    }

    [Test]
    public void AugAssignSlotField_AndBuiltins()
    {
        const string body =
            "from pymcu.types import int8\n\n" +
            "class Box:\n" +
            "    def __init__(self, x: uint8, y: uint8):\n" +
            "        self.x = x\n" +
            "        self.y = y\n\n" +
            "def run(s: uint8):\n" +
            "    b = Box(s, s + 10)\n" +
            "    b.x += 3\n" +                       // aug-assign on a slot field outside a method
            "    b.y -= 2\n" +
            "    print(b.x)\n" +                     // 8
            "    print(b.y)\n" +                     // 13
            "    print(max(b.x, b.y))\n" +           // 13
            "    print(min(b.x, b.y))\n" +           // 8
            "    print(abs(int8(0) - int8(s)))\n";   // abs(-5) = 5
        RunSeed(body, 5, 5).Should().Equal(8, 13, 13, 8, 5);
    }

    [Test]
    public void SignedCompareAgainstZero()
    {
        const string body =
            "from pymcu.types import int8\n\n" +
            "def run(s: uint8):\n" +
            "    n: int8 = int8(0) - int8(s)\n" +   // -5
            "    print(1 if n < 0 else 0)\n" +       // 1
            "    print(1 if n < int8(1) else 0)\n" + // 1
            "    m: int8 = int8(s)\n" +              // 5
            "    print(1 if m < 0 else 0)\n";        // 0
        RunSeed(body, 5, 3).Should().Equal(1, 1, 0);
    }

    [Test]
    public void Uint32AsSecondArg()
    {
        // A 32-bit value passed as a non-first argument must get its own 4-register block and not
        // clobber arg0 (the old fixed [R24,R22,R20,R18] layout put it in R22:R23:R24:R25).
        const string body =
            "from pymcu.types import uint32\n\n" +
            "def combine(a: uint8, b: uint32) -> uint32:\n" +
            "    return b + uint32(a)\n\n" +
            "def run(s: uint8):\n" +
            "    r: uint32 = combine(s, uint32(s) * 1000000)\n" +  // 10_000_000 + 10
            "    print(r)\n";
        RunSeed(body, 10, 1).Should().Equal(10000010);
    }

    [Test]
    public void MultiFieldZca_Uint32Field()
    {
        // The mutator bump(self, d: uint32) passes d as a 32-bit second argument; with the ABI
        // fix the slot field accumulates correctly.
        const string body =
            "from pymcu.types import uint32\n\n" +
            "class Ctr:\n" +
            "    def __init__(self, n: uint32, k: uint8):\n" +
            "        self.n = n\n" +
            "        self.k = k\n\n" +
            "    def bump(self, d: uint32):\n" +
            "        self.n = self.n + d\n\n" +
            "    def get(self) -> uint32:\n" +
            "        return self.n\n\n" +
            "def run(s: uint8):\n" +
            "    c = Ctr(uint32(s) * 1000000, s)\n" +   // 10_000_000
            "    c.bump(2345678)\n" +                    // 12_345_678
            "    print(c.get())\n" +
            "    print(c.n)\n" +
            "    print(c.k)\n";                          // 10
        RunSeed(body, 10, 3).Should().Equal(12345678, 12345678, 10);
    }

    [Test]
    public void Int32SignedArithmetic()
    {
        const string body =
            "from pymcu.types import int32\n\n" +
            "def run(s: uint8):\n" +
            "    a: int32 = int32(0) - int32(s) * 1000000\n" +   // -7000000
            "    print(a)\n" +
            "    print(a // 3)\n" +          // floor(-7000000/3) = -2333334
            "    print(a % 3)\n" +           // Python floor mod = 2
            "    print(1 if a < 0 else 0)\n" + // 1
            "    b: int32 = a + 7000005\n" +
            "    print(b)\n";                 // 5
        var got = RunSeed(body, 7, 5);
        TestContext.WriteLine("GOT=" + string.Join(",", got));
        got.Should().Equal(-7000000, -2333334, 2, 1, 5);
    }

    [Test]
    public void InstanceArrayMethodAndField()
    {
        const string body =
            "from pymcu.types import uint16\n\n" +
            "class Pt:\n" +
            "    def __init__(self, x: uint8, y: uint8):\n" +
            "        self.x = x\n" +
            "        self.y = y\n\n" +
            "    def sum(self) -> uint16:\n" +
            "        return uint16(self.x) + uint16(self.y)\n\n" +
            "def run(s: uint8):\n" +
            "    pts: Pt[3] = [Pt(0, 0), Pt(0, 0), Pt(0, 0)]\n" +
            "    pts[0] = Pt(s, s + 1)\n" +
            "    pts[1] = Pt(s + 2, s + 3)\n" +
            "    pts[2] = Pt(s + 4, s + 5)\n" +
            "    print(pts[0].sum())\n" +   // 11
            "    print(pts[2].sum())\n" +   // 19
            "    i: uint8 = s - 4\n" +
            "    print(pts[i].sum())\n" +   // pts[1] = 15
            "    print(pts[1].x)\n" +       // 7
            "    pts[1].x = 99\n" +         // direct field write on an element
            "    pts[i].y += 1\n" +         // aug-assign field via runtime index (i=1)
            "    print(pts[1].x)\n" +       // 99
            "    print(pts[1].y)\n";        // 8+1 = 9
        RunSeed(body, 5, 6).Should().Equal(11, 19, 15, 7, 99, 9);
    }

    [Test]
    public void Uint32GlobalAndBytearrayParam()
    {
        const string body =
            "from pymcu.types import uint32\n\n" +
            "counter: uint32 = 0\n\n" +
            "def add(n: uint32):\n" +
            "    global counter\n" +
            "    counter = counter + n\n\n" +
            "def fill(b: bytearray, base: uint8) -> uint8:\n" +
            "    i: uint8 = 0\n" +
            "    while i < 4:\n" +
            "        b[i] = base + i\n" +
            "        i += 1\n" +
            "    return i\n\n" +
            "def run(s: uint8):\n" +
            "    add(uint32(s) * 1000000)\n" +   // 7000000
            "    add(2345678)\n" +                // 9345678
            "    print(counter)\n" +
            "    buf: uint8[4] = [0, 0, 0, 0]\n" +
            "    n: uint8 = fill(buf, s)\n" +
            "    print(n)\n" +                     // 4
            "    print(buf[0])\n" +                // 7
            "    print(buf[3])\n";                 // 10
        var got = RunSeed(body, 7, 4);
        TestContext.WriteLine("GOT=" + string.Join(",", got));
        got.Should().Equal(9345678, 4, 7, 10);
    }

    [Test]
    public void BytesIterTruthinessNegStep()
    {
        const string body =
            "def run(s: uint8):\n" +
            "    total: uint8 = 0\n" +
            "    for b in b\"\\x01\\x02\\x03\\x04\":\n" +
            "        total += b\n" +
            "    print(total)\n" +              // 10
            "    x: uint8 = s\n" +
            "    print(1 if x else 0)\n" +      // 1 (truthy)
            "    y: uint8 = 0\n" +
            "    print(1 if not y else 0)\n" +  // 1
            "    acc: uint8 = 0\n" +
            "    for i in range(s, 0, -1):\n" + // 3,2,1
            "        acc += i\n" +
            "    print(acc)\n";                 // 6
        var got = RunSeed(body, 3, 4);
        TestContext.WriteLine("GOT=" + string.Join(",", got));
        got.Should().Equal(10, 1, 1, 6);
    }

    [Test]
    public void Uint16DivModRuntimeAndChainedCompare()
    {
        const string body =
            "from pymcu.types import uint16\n\n" +
            "def run(s: uint8):\n" +
            "    a: uint16 = uint16(s) * 1000\n" +   // 7000
            "    d: uint16 = uint16(s)\n" +          // 7
            "    print(a // d)\n" +                   // 1000
            "    print(a % d)\n" +                    // 0
            "    print(a // 13)\n" +                  // 538
            "    print(a % 13)\n" +                   // 6
            "    print(1 if 0 < a < 10000 else 0)\n" + // 1
            "    print(1 if 7000 <= a < 7001 else 0)\n"; // 1
        var got = RunSeed(body, 7, 6);
        TestContext.WriteLine("GOT=" + string.Join(",", got));
        got.Should().Equal(1000, 0, 538, 6, 1, 1);
    }

    [Test]
    public void NoMethodStruct_WideField()
    {
        // A multi-field struct with no methods uses the flattened-field path, which hard-coded
        // uint8 and truncated a uint16 field (500 read back as 244). Standalone and as a Class[N]
        // element (the array needs the contiguous slot layout even without methods).
        const string body =
            "from pymcu.types import uint16\n\n" +
            "class Acc:\n" +
            "    def __init__(self, total: uint16, tag: uint8):\n" +
            "        self.total = total\n" +
            "        self.tag = tag\n\n" +
            "def run(s: uint8):\n" +
            "    b = Acc(uint16(s) * 100, s + 1)\n" +
            "    print(b.total)\n" +   // 500
            "    b.total += 7\n" +
            "    print(b.total)\n" +   // 507
            "    accs: Acc[2] = [Acc(0, 0), Acc(0, 0)]\n" +
            "    accs[0] = Acc(uint16(s) * 100, s)\n" +       // 500, 5
            "    accs[1] = Acc(uint16(s) * 200, s + 1)\n" +   // 1000, 6
            "    i: uint8 = s - 5\n" +
            "    accs[i].total += 1234\n" +                   // accs[0] = 1734
            "    print(accs[0].total)\n" +   // 1734
            "    print(accs[1].total)\n" +   // 1000
            "    print(accs[1].tag)\n";      // 6
        var got = RunSeed(body, 5, 5);
        TestContext.WriteLine("GOT=" + string.Join(",", got));
        got.Should().Equal(500, 507, 1734, 1000, 6);
    }

    [Test]
    public void OperatorOverloadResultMethod()
    {
        // A65: an operator dunder returning a new slot-class instance, then a method call on the
        // result. The result must be materialized into a slot so the method gets a self pointer
        // (it used to pass the flattened fields, returning garbage: mag() gave 3 instead of 18).
        const string body =
            "from pymcu.types import uint16\n\n" +
            "class Vec:\n" +
            "    def __init__(self, x: uint8, y: uint8):\n" +
            "        self.x = x\n" +
            "        self.y = y\n\n" +
            "    def __add__(self, other: Vec) -> Vec:\n" +
            "        return Vec(self.x + other.x, self.y + other.y)\n\n" +
            "    def mag(self) -> uint16:\n" +
            "        return uint16(self.x) + uint16(self.y)\n\n" +
            "def run(s: uint8):\n" +
            "    a = Vec(s, s + 1)\n" +       // (3,4)
            "    b = Vec(s + 2, s + 3)\n" +   // (5,6)
            "    c = a + b\n" +               // (8,10)
            "    print(c.x)\n" +              // 8
            "    print(c.y)\n" +              // 10
            "    print(c.mag())\n" +          // 18
            "    print(a.mag())\n" +          // 7
            "    print(b.mag())\n";           // 11
        var got = RunSeed(body, 3, 5);
        TestContext.WriteLine("GOT=" + string.Join(",", got));
        got.Should().Equal(8, 10, 18, 7, 11);
    }

    [Test]
    public void InheritedFieldsInOverriddenMethod()
    {
        // A66: a subclass with no __init__ of its own inherits the base's fields; an overridden
        // method must still resolve them. The subclass's field layout was empty, so `self.a`
        // errored "not a member of a numeric value". Only inherited when the base is a slot class.
        const string body =
            "from pymcu.types import uint16\n\n" +
            "class Base:\n" +
            "    def __init__(self, a: uint8, b: uint8):\n" +
            "        self.a = a\n" +
            "        self.b = b\n\n" +
            "    def combine(self) -> uint16:\n" +
            "        return uint16(self.a) + uint16(self.b)\n\n" +
            "class Derived(Base):\n" +
            "    def combine(self) -> uint16:\n" +
            "        return uint16(self.a) * uint16(self.b)\n\n" +
            "def run(s: uint8):\n" +
            "    base = Base(s, s + 1)\n" +
            "    der = Derived(s, s + 1)\n" +
            "    print(base.combine())\n" +   // 13
            "    print(der.combine())\n" +    // 42 (overridden)
            "    print(der.a)\n";             // 6 (inherited field)
        RunSeed(body, 6, 3).Should().Equal(13, 42, 6);
    }

    [Test]
    public void PropertyGetterSetter()
    {
        const string body =
            "from pymcu.types import uint16\n\n" +
            "class Temp:\n" +
            "    def __init__(self, raw: uint8):\n" +
            "        self._raw = raw\n\n" +
            "    @property\n" +
            "    def celsius(self) -> uint16:\n" +
            "        return uint16(self._raw) * 2\n\n" +
            "    @celsius.setter\n" +
            "    def celsius(self, v: uint8):\n" +
            "        self._raw = v // 2\n\n" +
            "def run(s: uint8):\n" +
            "    t = Temp(s)\n" +          // _raw=10
            "    print(t.celsius)\n" +     // 20 (getter)
            "    t.celsius = 40\n" +       // setter -> _raw=20
            "    print(t.celsius)\n" +     // 40
            "    print(t._raw)\n";         // 20
        var got = RunSeed(body, 10, 3);
        TestContext.WriteLine("GOT=" + string.Join(",", got));
        got.Should().Equal(20, 40, 20);
    }

    [Test]
    public void OperatorPrecedence()
    {
        const string body =
            "def run(s: uint8):\n" +
            "    print(2 + 3 * 4 - 10 // 2)\n" +              // 9
            "    print(1 << 3 | 2 & 3)\n" +                  // 8 | (2&3) = 10
            "    print(1 if (s > 5 and s < 10) or s == 0 else 0)\n"; // 1
        RunSeed(body, 7, 3).Should().Equal(9, 10, 1);
    }

    [Test]
    public void NestedTernary()
    {
        // s=5 -> middle branch. classify: <3 ->100, <7 ->200, else 300
        const string body =
            "def run(s: uint8):\n" +
            "    r: uint8 = 100 if s < 3 else (200 if s < 7 else 250)\n" +
            "    print(r)\n";        // 200
        RunSeed(body, 5, 1).Should().Equal(200);
    }
}
