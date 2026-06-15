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
