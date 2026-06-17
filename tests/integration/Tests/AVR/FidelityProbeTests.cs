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
    public void FiveLevelInheritanceChain()
    {
        // Five levels (L0..L4), each level's __init__ chaining via super().__init__(), fields
        // inherited across all five, a leaf override of kind(), a mutating method (go) invoked
        // twice, and template-method virtual dispatch (go calls self.kind()).
        const string body =
            "from pymcu.types import uint16\n\n" +
            "class L0:\n    def __init__(self, a: uint8):\n        self._a = a\n        self._n = 0\n" +
            "    def kind(self) -> uint16:\n        return 0\n" +
            "    def go(self) -> uint16:\n        self._n = self._n + 1\n        return self.kind() + self._n\n\n" +
            "class L1(L0):\n    def __init__(self, a: uint8):\n        super().__init__(a)\n        self._b = 2\n\n" +
            "class L2(L1):\n    def __init__(self, a: uint8):\n        super().__init__(a)\n        self._c = 3\n\n" +
            "class L3(L2):\n    def __init__(self, a: uint8):\n        super().__init__(a)\n        self._d = 4\n\n" +
            "class L4(L3):\n    def __init__(self, a: uint8):\n        super().__init__(a)\n" +
            "    def kind(self) -> uint16:\n        return uint16(self._a) * 100 + self._b + self._c + self._d\n\n" +
            "def run(s: uint8):\n    x = L4(s)\n" +
            "    print(x.go())\n" +   // n=1: 5*100+2+3+4 + 1 = 510
            "    print(x.go())\n" +   // n=2: 509 + 2 = 511
            "    print(x._a)\n" +     // 5
            "    print(x._d)\n";      // 4
        RunSeed(body, 5, 4).Should().Equal(510, 511, 5, 4);
    }

    [Test]
    public void DhtStyleThreeLevelInheritance()
    {
        // Real-world 3-level chain (SensorBase -> GenericDht -> Dht11): super().__init__ chains,
        // fields inherited across 3 levels, a 3-field slot, a mutating method, and a template-
        // method virtual dispatch (sample() calls self.read_raw(), overridden at the leaf).
        const string body =
            "from pymcu.types import uint16\n\n" +
            "class SensorBase:\n" +
            "    def __init__(self, pin: uint8):\n        self._pin = pin\n        self._reads = 0\n\n" +
            "    def read_raw(self) -> uint16:\n        return 0\n\n" +
            "    def sample(self) -> uint16:\n        self._reads = self._reads + 1\n        return self.read_raw()\n\n" +
            "class GenericDht(SensorBase):\n" +
            "    def __init__(self, pin: uint8, kind: uint8):\n        super().__init__(pin)\n        self._kind = kind\n\n" +
            "    def read_raw(self) -> uint16:\n        return uint16(self._pin) * 10 + self._kind\n\n" +
            "class Dht11(GenericDht):\n" +
            "    def __init__(self, pin: uint8):\n        super().__init__(pin, 1)\n\n" +
            "    def read_raw(self) -> uint16:\n        return uint16(self._pin) * 100 + self._kind + self._reads\n\n" +
            "def run(s: uint8):\n" +
            "    d = Dht11(s)\n" +
            "    print(d.sample())\n" +   // reads=1: 5*100 + 1 + 1 = 502
            "    print(d.sample())\n" +   // reads=2: 5*100 + 1 + 2 = 503
            "    print(d._pin)\n" +       // 5
            "    print(d._kind)\n";       // 1
        RunSeed(body, 5, 4).Should().Equal(502, 503, 5, 1);
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
    public void TrueDivisionReturnsFloat()
    {
        // Python 3: `/` always returns float, even for two ints. 5 / 2 == 2.5, 5 / 5 == 1.0.
        // PyMCU promotes integer operands to float (and warns that float routines are linked).
        const string src =
            "from pymcu.types import uint8\n" +
            "from pymcu.hal.uart import UART\n\n\n" +
            "def main():\n" +
            "    uart = UART(9600)\n" +
            "    uart.println(\"GO\")\n" +
            "    s: uint8 = uart.read_blocking()\n" +
            "    print(s / 2)\n" +       // 5 -> 2.5
            "    print(s / 5)\n" +       // 5 -> 1.0
            "    while True:\n        pass\n";
        var hex = PymcuCompiler.BuildSource(src);
        var uno = new ArduinoUnoSimulation();
        uno.WithHex(hex);
        uno.RunUntilSerial(uno.Serial, "GO\n", maxMs: 500);
        uno.Serial.InjectByte(5);
        uno.RunUntilSerial(uno.Serial, t => NL(t) >= 3, maxMs: 6000);
        var lines = uno.Serial.Text.Replace("\r", "").Split('\n');
        int start = Array.FindIndex(lines, l => l.Trim() == "GO");
        var got = new List<string>();
        for (int i = start + 1; i < lines.Length && got.Count < 2; i++)
            if (lines[i].Trim().Length > 0) got.Add(lines[i].Trim());
        got[0].Should().StartWith("2.5");
        got[1].Should().StartWith("1.0");
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
    public void TernaryWideCallBranch()
    {
        // A ternary branch that is a uint16-returning call: the result must stay 16-bit.
        // s=5: big() if s>3 -> 500; 7 if s>3 -> 7 (the wide call is on the not-taken side).
        const string body =
            "from pymcu.types import uint16\n\n" +
            "def big() -> uint16:\n" +
            "    return 500\n\n" +
            "def run(s: uint8):\n" +
            "    print(big() if s > 3 else 7)\n" +   // 500
            "    print(7 if s > 3 else big())\n";     // 7
        RunSeed(body, 5, 2).Should().Equal(500, 7);
    }

    [Test]
    public void ArgEvalOrder_LeftToRight()
    {
        // tick() returns 1,2,3,... on successive calls. Python evaluates arguments and
        // binary operands left-to-right, so diff(tick(), tick()) = diff(1,2) = -1 and
        // tick()*10 + tick() = 3*10 + 4 = 34. Right-to-left eval would give +1 and 43.
        const string body =
            "from pymcu.types import int16\n\n" +
            "_n: uint8 = 0\n\n" +
            "def tick() -> uint8:\n" +
            "    global _n\n" +
            "    _n += 1\n" +
            "    return _n\n\n" +
            "def diff(a: uint8, b: uint8) -> int16:\n" +
            "    return int16(a) - int16(b)\n\n" +
            "def run(s: uint8):\n" +
            "    print(diff(tick(), tick()))\n" +   // diff(1,2) = -1
            "    print(tick() * 10 + tick())\n";     // 3*10 + 4 = 34
        RunSeed(body, 5, 2).Should().Equal(-1, 34);
    }

    // ── @property stress battery ───────────────────────────────────────────────

    [Test]
    public void Property_InExpressionConditionAndLoop()
    {
        // getter recomputed each access; used in arithmetic, a loop accumulator, and a condition.
        const string body =
            "from pymcu.types import uint16\n\n" +
            "class Temp:\n" +
            "    def __init__(self, raw: uint8):\n" +
            "        self._raw = raw\n\n" +
            "    @property\n" +
            "    def celsius(self) -> uint16:\n" +
            "        return uint16(self._raw) * 2\n\n" +
            "def run(s: uint8):\n" +
            "    t = Temp(s)\n" +                       // raw=5 -> celsius=10
            "    print(t.celsius)\n" +                  // 10
            "    total: uint16 = 0\n" +
            "    i: uint8 = 0\n" +
            "    while i < 3:\n" +
            "        total += t.celsius\n" +            // 10*3
            "        i += 1\n" +
            "    print(total)\n" +                      // 30
            "    print(t.celsius + 100)\n" +            // 110
            "    print(1 if t.celsius > 5 else 0)\n";   // 1
        RunSeed(body, 5, 4).Should().Equal(10, 30, 110, 1);
    }

    [Test]
    public void Property_AugAssignThroughGetterSetter()
    {
        // b.val += 5 must read via the getter and write via the setter.
        const string body =
            "class Box:\n" +
            "    def __init__(self, v: uint8):\n" +
            "        self._v = v\n\n" +
            "    @property\n" +
            "    def val(self) -> uint8:\n" +
            "        return self._v\n\n" +
            "    @val.setter\n" +
            "    def val(self, x: uint8):\n" +
            "        self._v = x\n\n" +
            "def run(s: uint8):\n" +
            "    b = Box(s)\n" +
            "    b.val = 20\n" +
            "    print(b.val)\n" +    // 20
            "    b.val += 5\n" +
            "    print(b.val)\n";     // 25
        RunSeed(body, 5, 2).Should().Equal(20, 25);
    }

    [Test]
    public void Property_MultiFieldComputedWide()
    {
        // getter over TWO fields, producing a 16-bit product.
        const string body =
            "from pymcu.types import uint16\n\n" +
            "class Rect:\n" +
            "    def __init__(self, w: uint8, h: uint8):\n" +
            "        self._w = w\n" +
            "        self._h = h\n\n" +
            "    @property\n" +
            "    def area(self) -> uint16:\n" +
            "        return uint16(self._w) * uint16(self._h)\n\n" +
            "def run(s: uint8):\n" +
            "    r = Rect(s, 30)\n" +    // 5*30
            "    print(r.area)\n";       // 150
        RunSeed(body, 5, 1).Should().Equal(150);
    }

    [Test]
    public void Property_GetterCallsMethod()
    {
        // getter body invokes another method on self.
        const string body =
            "from pymcu.types import uint16\n\n" +
            "class Calc:\n" +
            "    def __init__(self, base: uint8):\n" +
            "        self._base = base\n\n" +
            "    def doubled(self) -> uint16:\n" +
            "        return uint16(self._base) * 2\n\n" +
            "    @property\n" +
            "    def result(self) -> uint16:\n" +
            "        return self.doubled() + 1\n\n" +
            "def run(s: uint8):\n" +
            "    c = Calc(s)\n" +      // base=5
            "    print(c.result)\n";  // 5*2+1 = 11
        RunSeed(body, 5, 1).Should().Equal(11);
    }

    [Test]
    public void Property_MultipleAndMutatingFields()
    {
        // no-arg __init__, two fields, a branching getter, methods that mutate fields.
        const string body =
            "from pymcu.types import uint16\n\n" +
            "class Acc:\n" +
            "    def __init__(self):\n" +
            "        self._sum = 0\n" +
            "        self._count = 0\n\n" +
            "    def add(self, v: uint8):\n" +
            "        self._sum += v\n" +
            "        self._count += 1\n\n" +
            "    @property\n" +
            "    def average(self) -> uint16:\n" +
            "        if self._count == 0:\n" +
            "            return 0\n" +
            "        return self._sum // self._count\n\n" +
            "def run(s: uint8):\n" +
            "    a = Acc()\n" +
            "    print(a.average)\n" +   // 0 (count==0)
            "    a.add(s)\n" +           // sum=5,count=1
            "    a.add(10)\n" +          // sum=15,count=2
            "    a.add(30)\n" +          // sum=45,count=3
            "    print(a.average)\n";    // 15
        RunSeed(body, 5, 2).Should().Equal(0, 15);
    }

    [Test]
    public void Property_AsArgAndIndex()
    {
        // property result used as a function argument and as an array index.
        const string body =
            "from pymcu.types import uint8\n\n" +
            "class P:\n" +
            "    def __init__(self, k: uint8):\n" +
            "        self._k = k\n\n" +
            "    @property\n" +
            "    def idx(self) -> uint8:\n" +
            "        return self._k + 1\n\n" +
            "def twice(x: uint8) -> uint8:\n" +
            "    return x * 2\n\n" +
            "def run(s: uint8):\n" +
            "    p = P(s)\n" +                  // k=2 -> idx=3
            "    arr: uint8[5]\n" +
            "    arr[0] = 11\n" +
            "    arr[1] = 22\n" +
            "    arr[2] = 33\n" +
            "    arr[3] = 44\n" +
            "    print(twice(p.idx))\n" +       // twice(3)=6
            "    print(arr[p.idx])\n";          // arr[3]=44
        RunSeed(body, 2, 2).Should().Equal(6, 44);
    }

    [Test]
    public void Property_SetterClampsAndChains()
    {
        // setter runs logic (clamp); multiple set/get cycles.
        const string body =
            "from pymcu.types import uint8\n\n" +
            "class Dimmer:\n" +
            "    def __init__(self):\n" +
            "        self._level = 0\n\n" +
            "    @property\n" +
            "    def level(self) -> uint8:\n" +
            "        return self._level\n\n" +
            "    @level.setter\n" +
            "    def level(self, v: uint8):\n" +
            "        self._level = v if v < 100 else 100\n\n" +
            "def run(s: uint8):\n" +
            "    d = Dimmer()\n" +
            "    d.level = s\n" +
            "    print(d.level)\n" +     // 5
            "    d.level = 200\n" +      // clamped to 100
            "    print(d.level)\n" +     // 100
            "    d.level = 42\n" +
            "    print(d.level)\n";      // 42
        RunSeed(body, 5, 3).Should().Equal(5, 100, 42);
    }

    // ── dunder / inheritance / vtable battery ──────────────────────────────────

    [Test]
    public void Dunder_ComparisonOperators()
    {
        // __lt__, __le__, __gt__, __eq__ used in conditions and printed directly.
        const string body =
            "from pymcu.types import uint16\n\n" +
            "class Money:\n" +
            "    def __init__(self, c: uint16):\n" +
            "        self._c = c\n\n" +
            "    def __lt__(self, o: Money) -> bool:\n" +
            "        return self._c < o._c\n\n" +
            "    def __eq__(self, o: Money) -> bool:\n" +
            "        return self._c == o._c\n\n" +
            "def run(s: uint8):\n" +
            "    a = Money(uint16(s) * 100)\n" +   // 500
            "    b = Money(300)\n" +
            "    print(1 if a < b else 0)\n" +     // 0
            "    print(1 if b < a else 0)\n" +     // 1
            "    print(1 if a == b else 0)\n" +    // 0
            "    print(1 if a == a else 0)\n";     // 1
        RunSeed(body, 5, 4).Should().Equal(0, 1, 0, 1);
    }

    [Test]
    public void Dunder_GetSetItemAndLen()
    {
        // custom __getitem__, __setitem__ and __len__ on a wrapper around a fixed array.
        const string body =
            "from pymcu.types import uint8\n\n" +
            "class Buf:\n" +
            "    def __init__(self):\n" +
            "        self._data: uint8[4]\n" +
            "        self._n = 4\n\n" +
            "    def __getitem__(self, i: uint8) -> uint8:\n" +
            "        return self._data[i]\n\n" +
            "    def __setitem__(self, i: uint8, v: uint8):\n" +
            "        self._data[i] = v\n\n" +
            "    def __len__(self) -> uint8:\n" +
            "        return self._n\n\n" +
            "def run(s: uint8):\n" +
            "    b = Buf()\n" +
            "    b[0] = s\n" +
            "    b[1] = s + 10\n" +
            "    print(b[0])\n" +      // 5
            "    print(b[1])\n" +      // 15
            "    print(len(b))\n";     // 4
        RunSeed(body, 5, 3).Should().Equal(5, 15, 4);
    }

    [Test]
    public void Inherit_SuperInitAndOverride()
    {
        // super().__init__() sets an inherited field; the override reads both fields.
        const string body =
            "from pymcu.types import uint16\n\n" +
            "class Animal:\n" +
            "    def __init__(self, legs: uint8):\n" +
            "        self._legs = legs\n\n" +
            "    def describe(self) -> uint16:\n" +
            "        return self._legs\n\n" +
            "class Dog(Animal):\n" +
            "    def __init__(self, name: uint8):\n" +
            "        super().__init__(4)\n" +
            "        self._name = name\n\n" +
            "    def describe(self) -> uint16:\n" +
            "        return uint16(self._legs) * 10 + self._name\n\n" +
            "def run(s: uint8):\n" +
            "    d = Dog(s)\n" +          // name=5, legs=4
            "    print(d.describe())\n";  // 4*10+5 = 45
        RunSeed(body, 5, 1).Should().Equal(45);
    }

    [Test]
    public void Inherit_TemplateMethodDispatch()
    {
        // The vtable test: Shape.total() calls self.unit(); Square overrides unit(). Calling
        // total() on a Square must dispatch to Square.unit() (4), not Shape.unit() (1).
        const string body =
            "from pymcu.types import uint16\n\n" +
            "class Shape:\n" +
            "    def __init__(self, n: uint8):\n" +
            "        self._n = n\n\n" +
            "    def unit(self) -> uint16:\n" +
            "        return 1\n\n" +
            "    def total(self) -> uint16:\n" +
            "        return uint16(self._n) * self.unit()\n\n" +
            "class Square(Shape):\n" +
            "    def unit(self) -> uint16:\n" +
            "        return 4\n\n" +
            "def run(s: uint8):\n" +
            "    sq = Square(s)\n" +     // n=5
            "    print(sq.total())\n" + // 5*4 = 20 (Square.unit)
            "    sh = Shape(s)\n" +
            "    print(sh.total())\n";  // 5*1 = 5  (Shape.unit)
        RunSeed(body, 5, 2).Should().Equal(20, 5);
    }

    [Test]
    public void Inherit_MultiLevelResolution()
    {
        // A -> B -> C. C inherits __init__ from A and kind() from B (overriding A.kind()).
        const string body =
            "from pymcu.types import uint8\n\n" +
            "class A:\n" +
            "    def __init__(self, v: uint8):\n" +
            "        self._v = v\n\n" +
            "    def kind(self) -> uint8:\n" +
            "        return 1\n\n" +
            "class B(A):\n" +
            "    def kind(self) -> uint8:\n" +
            "        return 2\n\n" +
            "class C(B):\n" +
            "    def bump(self) -> uint8:\n" +
            "        return self.kind() + 10\n\n" +
            "def run(s: uint8):\n" +
            "    c = C(s)\n" +
            "    print(c.kind())\n" +   // 2 (from B)
            "    print(c._v)\n" +       // 5 (init from A)
            "    print(c.bump())\n";    // 2 + 10 = 12 (self.kind() -> B.kind)
        RunSeed(body, 5, 3).Should().Equal(2, 5, 12);
    }

    [Test]
    public void Inherit_OverrideCallsSuperMethod()
    {
        // A subclass method overriding a base method while still invoking the base via super().
        // The base is a 2-field slot class so construction goes through the (working) slot path.
        const string body =
            "from pymcu.types import uint16\n\n" +
            "class Base:\n" +
            "    def __init__(self, v: uint8, w: uint8):\n" +
            "        self._v = v\n" +
            "        self._w = w\n\n" +
            "    def score(self) -> uint16:\n" +
            "        return uint16(self._v) * 2 + self._w\n\n" +
            "class Boosted(Base):\n" +
            "    def __init__(self, v: uint8):\n" +
            "        super().__init__(v, 3)\n\n" +
            "    def score(self) -> uint16:\n" +
            "        return super().score() + 100\n\n" +
            "def run(s: uint8):\n" +
            "    x = Boosted(s)\n" +     // v=5, w=3
            "    print(x.score())\n";   // (5*2 + 3) + 100 = 113
        RunSeed(body, 5, 1).Should().Equal(113);
    }

    [Test]
    public void AbsMinMax_WideValues()
    {
        // s=5: y=500, x=-500 -> abs=500; max(y,50)=500, min(y,50)=50.
        // abs/min/max used a bare uint8 result temp that truncated wide values (500 -> 244).
        // (y is materialized first so the multiply widens to its int16 target -- the fixed-width
        // intermediate `0 - s*100` is a separate, intentional wrap and not what this probes.)
        const string body =
            "from pymcu.types import int16\n\n" +
            "def run(s: uint8):\n" +
            "    y: int16 = s * 100\n" +
            "    x: int16 = 0 - y\n" +
            "    print(x)\n" +               // -500
            "    print(abs(x))\n" +          // 500
            "    print(max(y, 50))\n" +      // 500
            "    print(min(y, 50))\n";       // 50
        RunSeed(body, 5, 4).Should().Equal(-500, 500, 500, 50);
    }

    [Test]
    public void IndirectCallWideReturn()
    {
        // s=5: big(5)=500 (uint16) called through a Callable. A uint8 result temp at the
        // indirect call site would read only the low byte -> 244.
        const string body =
            "from pymcu.types import uint16, Callable\n\n" +
            "def big(x: uint8) -> uint16:\n" +
            "    return uint16(x) * 100\n\n" +
            "def run(s: uint8):\n" +
            "    f: Callable = big\n" +
            "    print(f(s))\n";   // 500
        RunSeed(body, 5, 1).Should().Equal(500);
    }

    [Test]
    public void SumOfWideElements()
    {
        // s=5: a=500, b=300 (both uint16). sum([a,b]) = 800. A uint8 accumulator temp
        // truncates the already-wide operands to 800 & 0xFF = 32.
        // (All-uint8 operands wrapping is consistent fixed-width behavior and not probed here.)
        const string body =
            "from pymcu.types import uint16\n\n" +
            "def run(s: uint8):\n" +
            "    a: uint16 = s * 100\n" +
            "    b: uint16 = 300\n" +
            "    print(sum([a, b]))\n";   // 800
        RunSeed(body, 5, 1).Should().Equal(800);
    }

    [Test]
    public void TernaryMixedWidthBranches()
    {
        // s=5: cond false -> picks the uint16 branch (500). If the result temp is typed
        // only from the true branch (uint8 const 7), the false branch 500 truncates -> 244.
        const string body =
            "from pymcu.types import uint16\n\n" +
            "def run(s: uint8):\n" +
            "    a: uint16 = 500\n" +
            "    print(7 if s > 100 else a)\n" +   // 500
            "    print(a if s > 100 else 7)\n";     // 7
        RunSeed(body, 5, 2).Should().Equal(500, 7);
    }

    [Test]
    public void AugAssignChain_Uint16()
    {
        // s=5: x=5; *=100 ->500; -=50 ->450; //=7 ->64; %=10 ->4
        const string body =
            "from pymcu.types import uint16\n\n" +
            "def run(s: uint8):\n" +
            "    x: uint16 = s\n" +
            "    x *= 100\n" +
            "    print(x)\n" +     // 500
            "    x -= 50\n" +
            "    print(x)\n" +     // 450
            "    x //= 7\n" +
            "    print(x)\n" +     // 64
            "    x %= 10\n" +
            "    print(x)\n";      // 4
        RunSeed(body, 5, 4).Should().Equal(500, 450, 64, 4);
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

    [Test]
    public void OrAndValueSemantics()
    {
        // Python `or`/`and` return an OPERAND, not a 0/1 bool.
        const string body =
            "def run(s: uint8):\n" +
            "    print(s or 99)\n" +          // 5 truthy -> 5
            "    print((s - s) or 99)\n" +    // 0 -> 99
            "    print(s and 7)\n" +          // 5 truthy -> 7
            "    print((s - s) and 7)\n";     // 0 -> 0
        RunSeed(body, 5, 4).Should().Equal(5, 99, 7, 0);
    }

    [Test]
    public void BitNotUint8()
    {
        // Python ~5 == -6 (infinite precision). On a fixed-width uint8 the faithful-ish
        // result is the 8-bit complement 250; check what PyMCU actually emits.
        const string body =
            "def run(s: uint8):\n" +
            "    print(~s)\n";
        RunSeed(body, 5, 1).Should().Equal(250);
    }

    [Test]
    public void AugAssignWrapOnStore()
    {
        // x is uint8 storage: 200 + 100 = 300 promotes, then truncates back to uint8 = 44.
        const string body =
            "def run(s: uint8):\n" +
            "    x: uint8 = 200\n" +
            "    x = x + s\n" +               // s=100 -> 300 -> store uint8 -> 44
            "    print(x)\n";
        RunSeed(body, 100, 1).Should().Equal(44);
    }

    [Test]
    public void RShiftSignedIsArithmetic()
    {
        // Python >> on a negative int is an arithmetic shift (floors): -8 >> 1 == -4.
        const string body =
            "from pymcu.types import int16\n\n" +
            "def run(s: uint8):\n" +
            "    n: int16 = 0 - s\n" +     // -8
            "    print(n >> 1)\n";         // -4 (arithmetic, not 32764 logical)
        RunSeed(body, 8, 1).Should().Equal(-4);
    }

    [Test]
    public void RangeNegativeStep()
    {
        const string body =
            "def run(s: uint8):\n" +
            "    acc: uint8 = 0\n" +
            "    for i in range(s, 0, -1):\n" +   // 5,4,3,2,1
            "        acc = acc + i\n" +
            "    print(acc)\n";                   // 15
        RunSeed(body, 5, 1).Should().Equal(15);
    }

    [Test]
    public void RangeStepTwo()
    {
        const string body =
            "def run(s: uint8):\n" +
            "    acc: uint8 = 0\n" +
            "    for i in range(0, s, 2):\n" +    // 0,2,4,6,8
            "        acc = acc + i\n" +
            "    print(acc)\n";                   // 20
        RunSeed(body, 10, 1).Should().Equal(20);
    }

    [Test]
    public void InlineClosureCapturesEnclosingVar()
    {
        // A nested @inline function reads a variable from the enclosing scope; the capture must
        // resolve to the caller's value (regression: it silently read 0, so add(10) gave 10).
        const string body =
            "from pymcu.types import inline\n\n" +
            "def run(s: uint8):\n" +
            "    base: uint8 = s\n" +
            "    @inline\n" +
            "    def add(x: uint8) -> uint8:\n" +
            "        return x + base\n" +
            "    print(add(10))\n";   // 5 + 10 = 15
        RunSeed(body, 5, 1).Should().Equal(15);
    }

    [Test]
    public void SliceToInferredArray()
    {
        // `b = a[lo:hi]` without an array annotation infers b as a fixed-size array and copies
        // the slice's elements; b[i] must read the sliced values (regression: it read 0).
        const string body =
            "def run(s: uint8):\n" +
            "    a: uint8[4] = [10, 20, 30, 40]\n" +
            "    a[0] = s\n" +
            "    b = a[1:3]\n" +
            "    print(b[0])\n" +   // 20
            "    print(b[1])\n";   // 30
        RunSeed(body, 5, 2).Should().Equal(20, 30);
    }

    [Test]
    public void InlineClosureNestedCapture()
    {
        // Two-level closure: inner() captures `base` from the plain function run AND `bonus` from
        // the enclosing @inline outer. Both captures must resolve (regression: bonus read 0 -> 15).
        const string body =
            "from pymcu.types import inline\n\n" +
            "def run(s: uint8):\n" +
            "    base: uint8 = s\n" +       // 5
            "    @inline\n" +
            "    def outer(x: uint8) -> uint8:\n" +
            "        bonus: uint8 = 100\n" +
            "        @inline\n" +
            "        def inner(y: uint8) -> uint8:\n" +
            "            return y + base + bonus\n" +   // capture base (run) + bonus (outer)
            "        return inner(x)\n" +
            "    print(outer(10))\n";       // 10 + 5 + 100 = 115
        RunSeed(body, 5, 1).Should().Equal(115);
    }

    [Test]
    public void InlineClosureNonlocalRebind()
    {
        // `nonlocal` rebinds the enclosing variable: Python prints 42.
        const string body =
            "from pymcu.types import inline\n\n" +
            "def run(s: uint8):\n" +
            "    base: uint8 = s\n" +
            "    @inline\n" +
            "    def setit():\n" +
            "        nonlocal base\n" +
            "        base = 42\n" +
            "    setit()\n" +
            "    print(base)\n";   // 42
        RunSeed(body, 5, 1).Should().Equal(42);
    }

    [Test]
    public void InlineClosureWriteMakesLocal()
    {
        // An inline that declares its OWN local shadowing a captured name must not clobber the
        // enclosing variable (Python: assignment without nonlocal makes a local).
        const string body =
            "from pymcu.types import inline\n\n" +
            "def run(s: uint8):\n" +
            "    base: uint8 = s\n" +          // 5
            "    @inline\n" +
            "    def f(x: uint8) -> uint8:\n" +
            "        base: uint8 = x + 1\n" +  // NEW local
            "        return base\n" +
            "    print(f(10))\n" +             // 11
            "    print(base)\n";              // 5 (unchanged)
        RunSeed(body, 5, 2).Should().Equal(11, 5);
    }

    [Test]
    public void InlineClosureCaptureByReference()
    {
        // Closures capture by reference: reassigning the enclosing var before the call must be
        // visible inside (Python prints 99, not 5).
        const string body =
            "from pymcu.types import inline\n\n" +
            "def run(s: uint8):\n" +
            "    base: uint8 = s\n" +    // 5
            "    @inline\n" +
            "    def f() -> uint8:\n" +
            "        return base\n" +
            "    base = 99\n" +
            "    print(f())\n";          // 99
        RunSeed(body, 5, 1).Should().Equal(99);
    }

    [Test]
    public void FStringToStreamFloatAndNegative()
    {
        const string src =
            "from pymcu.types import uint8, int16\n" +
            "from pymcu.hal.uart import UART\n\n\n" +
            "def main():\n" +
            "    uart = UART(9600)\n" +
            "    uart.println(\"GO\")\n" +
            "    s: uint8 = uart.read_blocking()\n" +
            "    neg: int16 = 0 - int16(s)\n" +
            "    f: float = float(s) / 2.0\n" +
            "    print(f\"neg={neg} f={f}!\")\n" +
            "    while True:\n        pass\n";
        var hex = PymcuCompiler.BuildSource(src);
        var uno = new ArduinoUnoSimulation();
        uno.WithHex(hex);
        uno.RunUntilSerial(uno.Serial, "GO\n", maxMs: 500);
        uno.Serial.InjectByte(5);
        uno.RunUntilSerial(uno.Serial, t => t.Contains("!"), maxMs: 6000);
        uno.Serial.Text.Should().Contain("neg=-5 f=2.5!");
    }

    [Test]
    public void FStringConstantInterpolation()
    {
        const string src =
            "from pymcu.types import uint8\n" +
            "from pymcu.hal.uart import UART\n\n\n" +
            "NAME: const[str] = \"PB5\"\n" +
            "def main():\n" +
            "    uart = UART(9600)\n" +
            "    uart.println(\"GO\")\n" +
            "    s: uint8 = uart.read_blocking()\n" +
            "    print(f\"pin {NAME} = {s}\")\n" +
            "    while True:\n        pass\n";
        var hex = PymcuCompiler.BuildSource(src);
        var uno = new ArduinoUnoSimulation();
        uno.WithHex(hex);
        uno.RunUntilSerial(uno.Serial, "GO\n", maxMs: 500);
        uno.Serial.InjectByte(7);
        uno.RunUntilSerial(uno.Serial, t => t.Contains("= 7"), maxMs: 6000);
        uno.Serial.Text.Should().Contain("pin PB5 = 7");
    }

    [Test]
    public void FStringToStream()
    {
        const string src =
            "from pymcu.types import uint8, uint16\n" +
            "from pymcu.hal.uart import UART\n\n\n" +
            "def main():\n" +
            "    uart = UART(9600)\n" +
            "    uart.println(\"GO\")\n" +
            "    s: uint8 = uart.read_blocking()\n" +
            "    n: uint16 = uint16(s) * 100\n" +
            "    print(f\"v={s} n={n}!\")\n" +
            "    while True:\n        pass\n";
        var hex = PymcuCompiler.BuildSource(src);
        var uno = new ArduinoUnoSimulation();
        uno.WithHex(hex);
        uno.RunUntilSerial(uno.Serial, "GO\n", maxMs: 500);
        uno.Serial.InjectByte(5);
        uno.RunUntilSerial(uno.Serial, t => t.Contains("!"), maxMs: 6000);
        uno.Serial.Text.Should().Contain("v=5 n=500!");
    }

    [Test]
    public void SpilledArgFromNestedCall()
    {
        // A spilled argument whose value comes from a nested call: the call result must survive
        // into the spill region. Regression: register-arg loads ran first and clobbered the temp
        // holding the call result, so the spilled arg read 0 (fixed by storing spilled args first).
        const string body =
            "def add1(x: uint8) -> uint8:\n    return x + 1\n" +
            "def f6(a: uint8, b: uint8, c: uint8, d: uint8, e: uint8, g: uint8) -> uint8:\n" +
            "    return a + b + c + d + e + g * 10\n" +
            "def run(s: uint8):\n" +
            "    print(f6(1, 1, 1, 1, 1, add1(s)))\n";   // g=add1(5)=6 -> 60; +5 = 65
        RunSeed(body, 5, 1).Should().Equal(65);
    }

    [Test]
    public void DefaultArgViaSpill()
    {
        // A default value that fills a spilled parameter must be stored to the spill region.
        const string body =
            "def f6(a: uint8, b: uint8, c: uint8, d: uint8, e: uint8, g: uint8 = 7) -> uint8:\n" +
            "    return a + b + c + d + e + g * 10\n" +
            "def run(s: uint8):\n" +
            "    print(f6(1, 1, 1, 1, 1))\n" +      // g default 7 -> 70+5 = 75
            "    print(f6(s, 0, 0, 0, 0, 3))\n";    // g=3 -> 30+5 = 35
        RunSeed(body, 5, 2).Should().Equal(75, 35);
    }

    [Test]
    public void MethodSelfPlusFiveArgsViaSpill()
    {
        // A method passes self as arg0; self + 5 user args = 6 → the 6th spills.
        const string body =
            "from pymcu.types import uint16\n\n" +
            "class Calc:\n" +
            "    def __init__(self, base: uint8):\n        self._base = base\n" +
            "    def combine(self, a: uint8, b: uint8, c: uint8, d: uint8, e: uint8) -> uint16:\n" +
            "        return uint16(self._base) + a + b * 2 + c * 4 + d * 8 + e * 16\n" +
            "def run(s: uint8):\n" +
            "    c = Calc(s)\n" +
            "    print(c.combine(1, 1, 1, 1, 1))\n";   // 5 + 1+2+4+8+16 = 36
        RunSeed(body, 5, 1).Should().Equal(36);
    }

    [Test]
    public void ConsecutiveSpillCalls()
    {
        // Two consecutive calls reuse the shared spill region; each must be independent.
        const string body =
            "def f6(a: uint8, b: uint8, c: uint8, d: uint8, e: uint8, g: uint8) -> uint16:\n" +
            "    return uint16(a) + b + c + d + e + g * 10\n" +
            "def run(s: uint8):\n" +
            "    x: uint16 = f6(1, 1, 1, 1, 1, s)\n" +   // 5 + s*10
            "    y: uint16 = f6(2, 2, 2, 2, 2, s)\n" +   // 10 + s*10
            "    print(x)\n" +    // s=3: 35
            "    print(y)\n";     // 40
        RunSeed(body, 3, 2).Should().Equal(35, 40);
    }

    [Test]
    public void BitNotUint16Width()
    {
        const string body =
            "from pymcu.types import uint16\n\n" +
            "def run(s: uint8):\n" +
            "    a: uint16 = s\n" +    // 5
            "    print(~a)\n";         // 16-bit complement: 0xFFFA = 65530
        RunSeed(body, 5, 1).Should().Equal(65530);
    }

    [Test]
    public void Uint32RuntimeDivMod()
    {
        const string body =
            "from pymcu.types import uint32\n\n" +
            "def run(s: uint8):\n" +
            "    a: uint32 = 1000000\n" +
            "    d: uint32 = uint32(s)\n" +    // runtime divisor = 7
            "    print(a % d)\n" +             // 1000000 % 7 = 1
            "    print(a // d)\n";             // 142857
        RunSeed(body, 7, 2).Should().Equal(1, 142857);
    }

    [Test]
    public void GlobalArrayMutationPersists()
    {
        const string body =
            "counts: uint8[4] = [0, 0, 0, 0]\n\n" +
            "def bump(i: uint8):\n" +
            "    counts[i] = counts[i] + 1\n" +
            "def run(s: uint8):\n" +
            "    bump(1)\n" +
            "    bump(1)\n" +
            "    bump(s)\n" +          // s=2
            "    print(counts[1])\n" + // 2
            "    print(counts[2])\n";  // 1
        RunSeed(body, 2, 2).Should().Equal(2, 1);
    }

    [Test]
    public void TryAroundValueContextDivision()
    {
        // Regression: a value-context division inside a try (no Call in the body, so VisitTry adds
        // no BranchOnError) reached its catch dispatcher only via the div-zero guard's SignalError.
        // The optimizer did not count SignalError.CatchLabel as a CFG edge, deleted the catch block
        // and left a dangling jump -> link failure (undefined label). Must build and catch (88).
        const string body =
            "def run(s: uint8):\n" +
            "    r: uint8 = 1\n" +
            "    try:\n" +
            "        r = 100 // s\n" +   // s=0 -> ZeroDivisionError; value-context, no Call in try
            "    except ZeroDivisionError:\n" +
            "        r = 88\n" +
            "    print(r)\n";
        RunSeed(body, 0, 1).Should().Equal(88);
    }

    [Test]
    public void ExceptionPropagatesThroughLevels()
    {
        // An error raised deep (c) and uncaught in the intermediate frame (bb) must propagate up
        // through every CanFail frame to the try in run().
        const string body =
            "def c(b: uint8) -> uint8:\n    return 100 // b\n" +
            "def bb(b: uint8) -> uint8:\n    return c(b) + 1\n" +
            "def run(s: uint8):\n" +
            "    try:\n" +
            "        print(bb(s))\n" +   // s=0 -> ZeroDivisionError propagates c->bb->run
            "    except ZeroDivisionError:\n" +
            "        print(55)\n";
        RunSeed(body, 0, 1).Should().Equal(55);
    }

    [Test]
    public void WrongTypeExceptionPropagatesToOuter()
    {
        const string body =
            "def run(s: uint8):\n" +
            "    try:\n" +
            "        try:\n" +
            "            print(100 // s)\n" +    // ZeroDivisionError
            "        except IndexError:\n" +      // wrong type -> does not catch
            "            print(1)\n" +
            "    except ZeroDivisionError:\n" +    // outer catches
            "        print(66)\n";
        RunSeed(body, 0, 1).Should().Equal(66);
    }

    [Test]
    public void FinallyRunsOnErrorPath()
    {
        const string body =
            "def run(s: uint8):\n" +
            "    try:\n" +
            "        print(100 // s)\n" +   // s=0 -> ZeroDivisionError
            "    except ZeroDivisionError:\n" +
            "        print(7)\n" +
            "    finally:\n" +
            "        print(9)\n";
        RunSeed(body, 0, 2).Should().Equal(7, 9);   // except runs, then finally
    }

    [Test]
    public void RaiseFromCalleeCaught()
    {
        const string body =
            "def mayfail(s: uint8) -> uint8:\n" +
            "    if s == 0:\n        raise ValueError\n" +
            "    return s * 2\n" +
            "def run(s: uint8):\n" +
            "    try:\n" +
            "        print(mayfail(s))\n" +   // s=0 -> ValueError propagates -> caught
            "    except ValueError:\n" +
            "        print(3)\n";
        RunSeed(body, 0, 1).Should().Equal(3);
    }

    [Test]
    public void TryElseRunsWhenNoException()
    {
        const string body =
            "def run(s: uint8):\n" +
            "    try:\n" +
            "        y: uint8 = 100 // s\n" +   // s=5 -> ok, no exception
            "    except ZeroDivisionError:\n" +
            "        print(1)\n" +
            "    else:\n" +
            "        print(99)\n";              // runs only if no exception
        RunSeed(body, 5, 1).Should().Equal(99);
    }

    [Test]
    public void TryElseSkippedOnException()
    {
        const string body =
            "def run(s: uint8):\n" +
            "    try:\n" +
            "        print(100 // s)\n" +   // s=0 -> exception
            "    except ZeroDivisionError:\n" +
            "        print(5)\n" +
            "    else:\n" +
            "        print(99)\n";          // must NOT run when an exception occurred
        RunSeed(body, 0, 1).Should().Equal(5);
    }

    [Test]
    public void ReraiseInExceptPropagates()
    {
        const string body =
            "def inner(s: uint8) -> uint8:\n" +
            "    try:\n" +
            "        return 100 // s\n" +
            "    except ZeroDivisionError:\n" +
            "        raise ValueError\n" +       // re-raise as a different type
            "def run(s: uint8):\n" +
            "    try:\n" +
            "        print(inner(s))\n" +
            "    except ValueError:\n" +
            "        print(42)\n";
        RunSeed(body, 0, 1).Should().Equal(42);
    }

    [Test]
    public void SecondExceptClauseMatches()
    {
        const string body =
            "def run(s: uint8):\n" +
            "    try:\n" +
            "        print(100 // s)\n" +
            "    except IndexError:\n" +     // no match
            "        print(1)\n" +
            "    except ZeroDivisionError:\n" +   // matches
            "        print(2)\n";
        RunSeed(body, 0, 1).Should().Equal(2);
    }

    [Test]
    public void FinallyRunsBeforeBreak()
    {
        const string body =
            "def run(s: uint8):\n" +
            "    total: uint8 = 0\n" +
            "    for i in range(s):\n" +
            "        try:\n" +
            "            if i == 2:\n                break\n" +
            "            total = total + i\n" +
            "        finally:\n" +
            "            total = total + 10\n" +
            "    print(total)\n";
        // i=0: +0,+10=10 ; i=1: +1,+10=21 ; i=2: break but finally +10 -> 31
        RunSeed(body, 5, 1).Should().Equal(31);
    }

    [Test]
    public void FinallyRunsBeforeContinue()
    {
        const string body =
            "def run(s: uint8):\n" +
            "    total: uint8 = 0\n" +
            "    for i in range(s):\n" +
            "        try:\n" +
            "            if i == 2:\n                continue\n" +
            "            total = total + i\n" +
            "        finally:\n" +
            "            total = total + 10\n" +
            "    print(total)\n";
        // each iter finally +10 (5x=50); body adds 0+1+3+4=8 (i=2 skipped) -> 58
        RunSeed(body, 5, 1).Should().Equal(58);
    }

    [Test]
    public void ErrorCodePreservedThroughFinally()
    {
        // The error code lives in R22 while propagating. A finally that runs work (a print, which
        // calls a uart routine) between the raise and the propagation must not clobber it, or the
        // wrong handler is selected.
        const string body =
            "def f(s: uint8) -> uint8:\n" +
            "    try:\n" +
            "        return 100 // s\n" +    // s=0 -> ZeroDivisionError (code 6)
            "    finally:\n" +
            "        print(8)\n" +            // runs a uart routine: must preserve R22
            "def run(s: uint8):\n" +
            "    try:\n" +
            "        print(f(s))\n" +
            "    except ValueError:\n" +      // wrong type
            "        print(1)\n" +
            "    except ZeroDivisionError:\n" +  // correct
            "        print(2)\n";
        RunSeed(body, 0, 2).Should().Equal(8, 2);
    }

    [Test]
    public void RaiseInElsePropagates()
    {
        // A raise in the else block is NOT caught by this try (Python); an outer try catches it.
        const string body =
            "def run(s: uint8):\n" +
            "    try:\n" +
            "        try:\n" +
            "            y: uint8 = 100 // s\n" +   // s=5 -> ok, else runs
            "        except ZeroDivisionError:\n" +
            "            print(1)\n" +
            "        else:\n" +
            "            raise ValueError\n" +       // not caught by inner; propagates
            "    except ValueError:\n" +
            "        print(9)\n";
        RunSeed(body, 5, 1).Should().Equal(9);
    }

    [Test]
    public void RaiseInFinallyOverrides()
    {
        const string body =
            "def f(s: uint8) -> uint8:\n" +
            "    try:\n" +
            "        return s\n" +              // no exception in body
            "    finally:\n" +
            "        if s == 0:\n            raise ValueError\n" +   // finally raises
            "def run(s: uint8):\n" +
            "    try:\n" +
            "        print(f(s))\n" +
            "    except ValueError:\n" +
            "        print(7)\n";
        RunSeed(body, 0, 1).Should().Equal(7);
    }

    [Test]
    public void RecoverAndContinueAfterTry()
    {
        const string body =
            "def run(s: uint8):\n" +
            "    try:\n" +
            "        print(100 // s)\n" +   // s=0 -> caught
            "    except ZeroDivisionError:\n" +
            "        print(1)\n" +
            "    print(99)\n";              // runs after the try regardless
        RunSeed(body, 0, 2).Should().Equal(1, 99);
    }

    [Test]
    public void TryInLoopRecoversEachIteration()
    {
        // try/except inside a loop: the T-flag must be clean each iteration so a caught error in
        // one iteration does not leak into the next.
        const string body =
            "def run(s: uint8):\n" +
            "    total: uint8 = 0\n" +
            "    for i in range(s):\n" +
            "        try:\n" +
            "            total = total + 100 // i\n" +   // i=0 raises; i=1,2,3 -> 100,50,33
            "        except ZeroDivisionError:\n" +
            "            total = total + 1\n" +          // i=0 -> +1
            "    print(total)\n";
        RunSeed(body, 4, 1).Should().Equal(184);   // 1 + 100 + 50 + 33
    }

    [Test]
    public void SequentialTryBlocks()
    {
        // Two sequential try blocks: T must be reset after the first so the second works.
        const string body =
            "def run(s: uint8):\n" +
            "    try:\n" +
            "        print(100 // s)\n" +
            "    except ZeroDivisionError:\n" +
            "        print(1)\n" +
            "    try:\n" +
            "        print(50 // s)\n" +
            "    except ZeroDivisionError:\n" +
            "        print(2)\n";
        RunSeed(body, 0, 2).Should().Equal(1, 2);
    }

    [Test]
    public void ReturnInExceptHandlerRunsFinally()
    {
        // return inside an except handler must still run the finally (the flagged limitation).
        const string body =
            "def f(s: uint8) -> uint8:\n" +
            "    try:\n" +
            "        x: uint8 = 100 // s\n" +   // s=0 -> ZeroDivisionError
            "        return x\n" +
            "    except ZeroDivisionError:\n" +
            "        return 5\n" +              // return in handler -> finally must run first
            "    finally:\n" +
            "        print(8)\n" +
            "def run(s: uint8):\n" +
            "    print(f(s))\n";
        RunSeed(body, 0, 2).Should().Equal(8, 5);
    }

    [Test]
    public void RaiseFromMethodCaught()
    {
        const string body =
            "class Sensor:\n" +
            "    def __init__(self):\n        self._x = 0\n" +
            "    def read(self, s: uint8) -> uint8:\n        return 100 // s\n" +   // s=0 raises
            "def run(s: uint8):\n" +
            "    sensor = Sensor()\n" +
            "    try:\n" +
            "        print(sensor.read(s))\n" +
            "    except ZeroDivisionError:\n" +
            "        print(4)\n";
        RunSeed(body, 0, 1).Should().Equal(4);
    }

    [Test]
    public void NestedFinallyOnReturn()
    {
        // return through two nested try-with-finally levels runs both, innermost first.
        const string body =
            "def f(s: uint8) -> uint8:\n" +
            "    try:\n" +
            "        try:\n" +
            "            return s\n" +
            "        finally:\n" +
            "            print(1)\n" +    // inner finally first
            "    finally:\n" +
            "        print(2)\n" +        // then outer
            "def run(s: uint8):\n" +
            "    print(f(s))\n";
        RunSeed(body, 5, 3).Should().Equal(1, 2, 5);
    }

    [Test]
    public void NestedFinallyOnBreak()
    {
        // break through two nested finally levels runs both.
        const string body =
            "def run(s: uint8):\n" +
            "    t: uint8 = 0\n" +
            "    for i in range(s):\n" +
            "        try:\n" +
            "            try:\n" +
            "                if i == 1:\n                    break\n" +
            "                t = t + i\n" +
            "            finally:\n" +
            "                t = t + 10\n" +     // inner
            "        finally:\n" +
            "            t = t + 100\n" +        // outer
            "    print(t)\n";
        // i=0: +0,+10,+100=110 ; i=1: break -> +10,+100 -> 220
        RunSeed(body, 5, 1).Should().Equal(220);
    }

    [Test]
    public void RaiseInConstructorCaught()
    {
        const string body =
            "class Thing:\n" +
            "    def __init__(self, s: uint8):\n        self._v = 100 // s\n" +   // s=0 raises
            "def run(s: uint8):\n" +
            "    try:\n" +
            "        t = Thing(s)\n" +
            "        print(t._v)\n" +
            "    except ZeroDivisionError:\n" +
            "        print(6)\n";
        RunSeed(body, 0, 1).Should().Equal(6);
    }

    [Test]
    public void BareReraisePreservesCodeAfterClobber()
    {
        // The handler runs a 2-arg call (its 2nd arg lands in R22, the error-code register) before
        // a bare raise. The re-raised exception must still be ZeroDivisionError, not garbage.
        const string body =
            "def add2(a: uint8, b: uint8) -> uint8:\n    return a + b\n" +
            "def inner(s: uint8) -> uint8:\n" +
            "    try:\n" +
            "        return 100 // s\n" +     // ZeroDivisionError (code 6)
            "    except ZeroDivisionError:\n" +
            "        print(add2(1, 2))\n" +   // clobbers R22 with arg b
            "        raise\n" +               // bare re-raise -> must still be ZeroDivisionError
            "def run(s: uint8):\n" +
            "    try:\n" +
            "        print(inner(s))\n" +
            "    except ValueError:\n" +
            "        print(1)\n" +            // wrong (if R22 clobbered to some other code)
            "    except ZeroDivisionError:\n" +
            "        print(7)\n";             // correct
        RunSeed(body, 0, 2).Should().Equal(3, 7);
    }

    [Test]
    public void BareReraise()
    {
        // `raise` with no argument re-raises the current exception (Python).
        const string body =
            "def inner(s: uint8) -> uint8:\n" +
            "    try:\n" +
            "        return 100 // s\n" +
            "    except ZeroDivisionError:\n" +
            "        raise\n" +              // bare re-raise
            "def run(s: uint8):\n" +
            "    try:\n" +
            "        print(inner(s))\n" +
            "    except ZeroDivisionError:\n" +
            "        print(7)\n";
        RunSeed(body, 0, 1).Should().Equal(7);
    }

    [Test]
    public void FinallyExceptionOverridesPending()
    {
        const string body =
            "def f(s: uint8):\n" +
            "    try:\n" +
            "        raise ValueError\n" +     // body raises ValueError
            "    finally:\n" +
            "        x: uint8 = 100 // s\n" +  // s=0 -> finally raises ZeroDivisionError (overrides)
            "def run(s: uint8):\n" +
            "    try:\n" +
            "        f(s)\n" +
            "    except ValueError:\n" +
            "        print(1)\n" +             // must NOT catch
            "    except ZeroDivisionError:\n" +
            "        print(2)\n";              // overriding exception
        RunSeed(body, 0, 1).Should().Equal(2);
    }

    [Test]
    public void FinallyRunsBeforePropagation()
    {
        const string body =
            "def f(s: uint8) -> uint8:\n" +
            "    try:\n" +
            "        return 100 // s\n" +   // s=0 -> exception
            "    finally:\n" +
            "        print(8)\n" +          // must run before the error propagates
            "def run(s: uint8):\n" +
            "    try:\n" +
            "        print(f(s))\n" +
            "    except ZeroDivisionError:\n" +
            "        print(3)\n";
        RunSeed(body, 0, 2).Should().Equal(8, 3);
    }

    [Test]
    public void FinallyRunsBeforeReturn()
    {
        const string body =
            "def f(s: uint8) -> uint8:\n" +
            "    try:\n" +
            "        return s * 2\n" +   // returns, but finally runs first
            "    finally:\n" +
            "        print(7)\n" +
            "def run(s: uint8):\n" +
            "    r: uint8 = f(s)\n" +
            "    print(r)\n";
        RunSeed(body, 5, 2).Should().Equal(7, 10);
    }

    [Test]
    public void InlineRaisePropagatesToCaller()
    {
        const string body =
            "from pymcu.types import inline\n\n" +
            "@inline\n" +
            "def checked(s: uint8) -> uint8:\n" +
            "    if s == 0:\n        raise ValueError\n" +
            "    return 100 // s\n" +
            "def run(s: uint8):\n" +
            "    try:\n" +
            "        print(checked(s))\n" +
            "    except ValueError:\n" +
            "        print(4)\n";
        RunSeed(body, 0, 1).Should().Equal(4);
    }

    [Test]
    public void UncaughtExceptionHalts()
    {
        // An UNCAUGHT runtime divide-by-zero must now halt (loud failure, Python-like) rather than
        // silently continue with garbage: the code after the faulting division must not run.
        const string src =
            "from pymcu.types import uint8\n" +
            "from pymcu.hal.uart import UART\n\n\n" +
            "def divide(a: uint8, b: uint8) -> uint8:\n" +
            "    return a // b\n" +
            "def main():\n" +
            "    uart = UART(9600)\n" +
            "    uart.println(\"GO\")\n" +
            "    s: uint8 = uart.read_blocking()\n" +
            "    r: uint8 = divide(100, s)\n" +   // s=0 -> uncaught ZeroDivisionError -> halt
            "    uart.println(\"AFTER\")\n" +      // must NOT print (device halted)
            "    while True:\n        pass\n";
        var hex = PymcuCompiler.BuildSource(src);
        var uno = new ArduinoUnoSimulation();
        uno.WithHex(hex);
        uno.RunUntilSerial(uno.Serial, "GO\n", maxMs: 500);
        uno.Serial.InjectByte(0);
        // The device halts at the unhandled-exception handler, so "AFTER" never arrives — the
        // wait is expected to time out. That timeout IS the pass condition (no silent continue).
        try { uno.RunUntilSerial(uno.Serial, t => t.Contains("AFTER"), maxMs: 300); }
        catch (TimeoutException) { }
        uno.Serial.Text.Should().NotContain("AFTER");
    }

    [Test]
    public void RuntimeDivByZeroRaises()
    {
        // Runtime divide-by-zero now raises ZeroDivisionError (Python fidelity), catchable here.
        const string body =
            "def run(s: uint8):\n" +
            "    a: uint8 = 100\n" +
            "    try:\n" +
            "        print(a // s)\n" +   // s=0 -> ZeroDivisionError
            "    except ZeroDivisionError:\n" +
            "        print(77)\n";
        RunSeed(body, 0, 1).Should().Equal(77);
    }

    [Test]
    public void RuntimeDivByNonZeroWorks()
    {
        const string body =
            "def run(s: uint8):\n" +
            "    a: uint8 = 100\n" +
            "    try:\n" +
            "        print(a // s)\n" +   // s=5 -> 20, no raise
            "    except ZeroDivisionError:\n" +
            "        print(77)\n";
        RunSeed(body, 5, 1).Should().Equal(20);
    }

    [Test]
    public void AugAssignInstanceField()
    {
        const string body =
            "class Counter:\n" +
            "    def __init__(self):\n        self._n = 0\n" +
            "    def add(self, v: uint8):\n        self._n += v\n" +
            "    def get(self) -> uint8:\n        return self._n\n" +
            "def run(s: uint8):\n" +
            "    c = Counter()\n" +
            "    c.add(s)\n" +
            "    c.add(10)\n" +
            "    print(c.get())\n";   // s=5: 5+10 = 15
        RunSeed(body, 5, 1).Should().Equal(15);
    }

    [Test]
    public void SliceWithStep()
    {
        const string body =
            "def run(s: uint8):\n" +
            "    a: uint8[6] = [10, 20, 30, 40, 50, 60]\n" +
            "    a[0] = s\n" +
            "    b = a[0:6:2]\n" +    // indices 0,2,4 -> [s,30,50]
            "    print(b[0])\n" +    // s=5
            "    print(b[1])\n" +    // 30
            "    print(b[2])\n";    // 50
        RunSeed(body, 5, 3).Should().Equal(5, 30, 50);
    }

    [Test]
    public void SixArgsViaSpill()
    {
        // Six uint8 args: the first five use R24,R22,R20,R18,R16; the sixth overflows to the SRAM
        // spill region. Each must arrive intact. Position-encode so a dropped/corrupted arg shows.
        const string body =
            "def f6(a: uint8, b: uint8, c: uint8, d: uint8, e: uint8, g: uint8) -> uint8:\n" +
            "    return a + b * 2 + c * 4 + d * 8 + e * 16 + g * 32\n" +
            "def run(s: uint8):\n" +
            "    print(f6(1, 1, 1, 1, 1, 1))\n" +   // 1+2+4+8+16+32 = 63
            "    print(f6(s, 0, 0, 0, 0, 1))\n";    // s + 32 ; s=5 -> 37
        RunSeed(body, 5, 2).Should().Equal(63, 37);
    }

    [Test]
    public void EightArgsViaSpill()
    {
        // Eight uint8 args: three overflow to SRAM (16-bit spill offsets exercised too).
        const string body =
            "def f8(a: uint8, b: uint8, c: uint8, d: uint8, e: uint8, f: uint8, g: uint8, h: uint8) -> uint16:\n" +
            "    return uint16(a) + b * 2 + c * 4 + d * 8 + e * 16 + f * 32 + g * 64 + h * 128\n" +
            "def run(s: uint8):\n" +
            "    print(f8(1, 1, 1, 1, 1, 1, 1, 1))\n";   // sum of powers 1..128 = 255
        RunSeed(body, 5, 1).Should().Equal(255);
    }

    [Test]
    public void SpilledArgsWith16BitMix()
    {
        // A uint16 arg in the spill region (2-byte spill offset) must round-trip.
        const string body =
            "from pymcu.types import uint16\n\n" +
            "def f(a: uint8, b: uint8, c: uint8, d: uint8, e: uint8, w: uint16) -> uint16:\n" +
            "    return uint16(a + b + c + d + e) + w\n" +
            "def run(s: uint8):\n" +
            "    print(f(1, 2, 3, 4, 5, 1000))\n";   // 15 + 1000 = 1015
        RunSeed(body, 5, 1).Should().Equal(1015);
    }

    [Test]
    public void InlineRuntimeIndexedLocalArray()
    {
        // A runtime-indexed local array inside an @inline function must be allocated as SRAM when
        // the function is expanded (regression: it hit "subscript must be a compile-time constant"
        // because the per-function prescan never saw the inlined callee's locals).
        const string body =
            "from pymcu.types import inline\n\n" +
            "@inline\n" +
            "def rev_sum(n: uint8) -> uint8:\n" +
            "    buf: uint8[8] = [0, 0, 0, 0, 0, 0, 0, 0]\n" +
            "    i: uint8 = 0\n" +
            "    while i < n:\n" +
            "        buf[i] = i * 2\n" +       // runtime index store
            "        i = i + 1\n" +
            "    acc: uint8 = 0\n" +
            "    j: uint8 = 0\n" +
            "    while j < n:\n" +
            "        acc = acc + buf[j]\n" +   // runtime index load
            "        j = j + 1\n" +
            "    return acc\n" +
            "def run(s: uint8):\n" +
            "    print(rev_sum(s))\n";          // s=5: 0+2+4+6+8 = 20
        RunSeed(body, 5, 1).Should().Equal(20);
    }

    [Test]
    public void LcdPrintStrFStringCompilesAndRuns()
    {
        // lcd.print_str(f"...") lowers to print_str("literal") + print_fmt(value,...) on the same
        // LCD instance. Verify it builds and the program reaches its UART banner (the LCD format
        // code is valid and executes), consistent with the existing LCD test rigor.
        const string src =
            "from pymcu.types import uint8, uint16\n" +
            "from pymcu.hal.uart import UART\n" +
            "from pymcu.drivers.lcd import LCD\n\n\n" +
            "def main():\n" +
            "    uart = UART(9600)\n" +
            "    lcd = LCD(rs=\"PD4\", en=\"PD5\", d4=\"PD6\", d5=\"PD7\", d6=\"PB0\", d7=\"PB1\")\n" +
            "    lcd.init()\n" +
            "    uart.println(\"GO\")\n" +
            "    s: uint8 = uart.read_blocking()\n" +
            "    n: uint16 = uint16(s) * 100\n" +
            "    lcd.print_str(f\"T={s} 0x{n:04x}\")\n" +
            "    uart.println(\"DONE\")\n" +
            "    while True:\n        pass\n";
        var hex = PymcuCompiler.BuildSource(src);
        var uno = new ArduinoUnoSimulation();
        uno.WithHex(hex);
        uno.RunUntilSerial(uno.Serial, "GO\n", maxMs: 1000);
        uno.Serial.InjectByte(7);
        uno.RunUntilSerial(uno.Serial, t => t.Contains("DONE"), maxMs: 1000);
        uno.Serial.Text.Should().Contain("DONE");
    }

    [Test]
    public void FiveArgsAllArrive()
    {
        // Five uint8 args fit R24,R22,R20,R18,R16 (all >= R16). Each must arrive — the call/callee
        // loops used to cap at 4 and silently dropped args 5+. Position-encode to expose a drop.
        const string body =
            "def f5(a: uint8, b: uint8, c: uint8, d: uint8, e: uint8) -> uint8:\n" +
            "    return a + b * 2 + c * 4 + d * 8 + e * 16\n" +
            "def run(s: uint8):\n" +
            "    print(f5(1, 1, 1, 1, 1))\n" +   // 1+2+4+8+16 = 31
            "    print(f5(s, 0, 0, 0, 1))\n";    // s + 16 ; s=5 -> 21
        RunSeed(body, 5, 2).Should().Equal(31, 21);
    }

    [Test]
    public void FStringFormatSpecs()
    {
        const string src =
            "from pymcu.types import uint8, uint16\n" +
            "from pymcu.hal.uart import UART\n\n\n" +
            "def main():\n" +
            "    uart = UART(9600)\n" +
            "    uart.println(\"GO\")\n" +
            "    s: uint8 = uart.read_blocking()\n" +
            "    n: uint16 = uint16(s) * 50\n" +    // 250 * 50 = ... s=10 -> 500
            "    print(f\"hex={s:02x} HEX={n:04X} bin={s:08b} pad={s:4d}!\")\n" +
            "    while True:\n        pass\n";
        var hex = PymcuCompiler.BuildSource(src);
        var uno = new ArduinoUnoSimulation();
        uno.WithHex(hex);
        uno.RunUntilSerial(uno.Serial, "GO\n", maxMs: 500);
        uno.Serial.InjectByte(10);
        uno.RunUntilSerial(uno.Serial, t => t.Contains("!"), maxMs: 6000);
        // s=10: hex=0a, n=500=0x1F4 -> 01F4, bin(10)=00001010, pad "  10"
        uno.Serial.Text.Should().Contain("hex=0a HEX=01F4 bin=00001010 pad=  10!");
    }

    [Test]
    public void SignedWidenToInt32()
    {
        const string body =
            "from pymcu.types import int16, int32\n\n" +
            "def run(s: uint8):\n" +
            "    neg: int16 = 0 - int16(s)\n" +   // -8
            "    w: int32 = int32(neg)\n" +
            "    print(neg)\n" +    // -8
            "    print(w)\n";      // -8
        RunSeed(body, 8, 2).Should().Equal(-8, -8);
    }

    [Test]
    public void FStringFormatSignedAndOctal()
    {
        const string src =
            "from pymcu.types import uint8, int16\n" +
            "from pymcu.hal.uart import UART\n\n\n" +
            "def main():\n" +
            "    uart = UART(9600)\n" +
            "    uart.println(\"GO\")\n" +
            "    s: uint8 = uart.read_blocking()\n" +
            "    neg: int16 = 0 - int16(s)\n" +    // -5
            "    print(f\"[{neg:d}][{neg:5d}][{s:o}]!\")\n" +
            "    while True:\n        pass\n";
        var hex = PymcuCompiler.BuildSource(src);
        var uno = new ArduinoUnoSimulation();
        uno.WithHex(hex);
        uno.RunUntilSerial(uno.Serial, "GO\n", maxMs: 500);
        uno.Serial.InjectByte(8);
        uno.RunUntilSerial(uno.Serial, t => t.Contains("!"), maxMs: 6000);
        // neg=-8: "-8"; ":5d" -> "   -8" (width 5); s=8 octal -> "10"
        uno.Serial.Text.Should().Contain("[-8][   -8][10]!");
    }

    [Test]
    public void MixedSignedUnsignedComparison()
    {
        // The real C gotcha: comparing a signed and an unsigned value. Python compares by value
        // (-5 < 200 is True); a C-style promotion to unsigned would give 65531 < 200 = False.
        const string body =
            "from pymcu.types import int16, uint16\n\n" +
            "def run(s: uint8):\n" +
            "    neg: int16 = 0 - int16(s)\n" +   // -5
            "    pos: uint16 = 200\n" +
            "    print(1 if neg < pos else 0)\n" +     // -5 < 200 -> 1
            "    print(1 if pos > neg else 0)\n";      // 200 > -5 -> 1
        RunSeed(body, 5, 2).Should().Equal(1, 1);
    }

    [Test]
    public void UartWriteStrAndPrintlnFString()
    {
        const string src =
            "from pymcu.types import uint8, uint16\n" +
            "from pymcu.hal.uart import UART\n\n\n" +
            "def main():\n" +
            "    uart = UART(9600)\n" +
            "    uart.println(\"GO\")\n" +
            "    s: uint8 = uart.read_blocking()\n" +
            "    n: uint16 = uint16(s) * 100\n" +
            "    uart.write_str(f\"a={s} b={n};\")\n" +
            "    uart.println(f\"line={s}\")\n" +
            "    while True:\n        pass\n";
        var hex = PymcuCompiler.BuildSource(src);
        var uno = new ArduinoUnoSimulation();
        uno.WithHex(hex);
        uno.RunUntilSerial(uno.Serial, "GO\n", maxMs: 500);
        uno.Serial.InjectByte(5);
        uno.RunUntilSerial(uno.Serial, t => NL(t) >= 2, maxMs: 6000);
        uno.Serial.Text.Should().Contain("a=5 b=500;");
        uno.Serial.Text.Should().Contain("line=5\n");
    }

    [Test]
    public void FloorDivMod_NegativeDividend()
    {
        // Python `//` floors toward -inf and `%` follows the divisor's sign (NOT C truncation).
        const string body =
            "from pymcu.types import int16\n\n" +
            "def run(s: uint8):\n" +
            "    n: int16 = 0 - s\n" +     // -7
            "    print(n // 2)\n" +        // Python floors: -4 (C trunc: -3)
            "    print(n % 2)\n";          // Python: 1 (C: -1)
        RunSeed(body, 7, 2).Should().Equal(-4, 1);
    }

    [Test]
    public void FloorDivMod_NegativeDivisor()
    {
        const string body =
            "from pymcu.types import int16\n\n" +
            "def run(s: uint8):\n" +
            "    n: int16 = s\n" +         // 7
            "    print(n // -2)\n" +       // Python: -4 (floor of -3.5)
            "    print(n % -2)\n";         // Python: -1 (sign follows divisor)
        RunSeed(body, 7, 2).Should().Equal(-4, -1);
    }

    [Test]
    public void Power_RuntimeBaseConstantExponent()
    {
        // s ** 2 lowers to repeated multiplication with promotion: 5*5 = 25, and a base that
        // overflows its width keeps the wide value (10 ** 3 = 1000, promoted past uint8).
        const string body =
            "def run(s: uint8):\n" +
            "    print(s ** 2)\n" +        // 5 -> 25
            "    print(s ** 3)\n" +        // 5 -> 125
            "    print(s ** 0)\n" +        // 1
            "    print(s ** 1)\n";         // 5
        RunSeed(body, 5, 4).Should().Equal(25, 125, 1, 5);
    }

    [Test]
    public void Power_WideResult()
    {
        // 10 ** 3 = 1000 must not truncate to uint8 (the promotion chain widens to uint16+).
        const string body =
            "def run(s: uint8):\n" +
            "    print(s ** 3)\n";         // 10 -> 1000
        RunSeed(body, 10, 1).Should().Equal(1000);
    }

    [Test]
    public void SwapUnpack()
    {
        // Tuple swap `a, b = b, a` evaluates the RHS tuple before binding (no clobber).
        const string body =
            "def run(s: uint8):\n" +
            "    a: uint8 = s\n" +
            "    b: uint8 = 100\n" +
            "    a, b = b, a\n" +
            "    print(a)\n" +             // 100
            "    print(b)\n";             // 5
        RunSeed(body, 5, 2).Should().Equal(100, 5);
    }

    [Test]
    public void ChainAssign()
    {
        // Chained assignment `a = b = s` binds both names to the same value.
        const string body =
            "def run(s: uint8):\n" +
            "    a: uint8 = 0\n" +
            "    b: uint8 = 0\n" +
            "    a = b = s\n" +
            "    print(a)\n" +             // 5
            "    print(b)\n";             // 5
        RunSeed(body, 5, 2).Should().Equal(5, 5);
    }

    [Test]
    public void Promote_Uint8AddWidensToUint16()
    {
        // Python fidelity: uint8 + uint8 promotes to uint16, so 255 + 45 = 300 (NOT 44).
        // s comes over UART so the add is a real runtime op, not constant-folded.
        const string body =
            "def run(s: uint8):\n" +
            "    r = s + 45\n" +   // 255 + 45 = 300 (promoted, no wrap)
            "    print(r)\n";
        RunSeed(body, 255, 1).Should().Equal(300);
    }

    [Test]
    public void Promote_ExplicitCastOptsOutToFixedWidth()
    {
        // The uint8(...) escape hatch forces a fixed-width 8-bit op: 255 + 45 = 300 wraps to 44.
        const string body =
            "def run(s: uint8):\n" +
            "    r: uint8 = uint8(s + 45)\n" +   // computed at 8-bit -> 0x12C & 0xFF = 44
            "    print(r)\n";
        RunSeed(body, 255, 1).Should().Equal(44);
    }

    [Test]
    public void Promote_Uint16WrapsOnExplicitStore()
    {
        // Promotion widens the temp, but the declared type is the STORAGE width: assigning the
        // promoted result back into a uint16 truncates. 65535 + 1 = 65536 -> stored uint16 = 0,
        // and the optimizer must mask its tracked constant so `e == 0` is true at runtime.
        const string body =
            "from pymcu.types import uint16\n\n" +
            "def run(s: uint8):\n" +
            "    e: uint16 = 65535\n" +
            "    e = e + s\n" +                       // s=1 -> 65536 wraps to 0 on store
            "    print(e)\n" +                        // 0
            "    print(1 if e == 0 else 9)\n";        // 1 (not folded to 9)
        RunSeed(body, 1, 2).Should().Equal(0, 1);
    }

    [Test]
    public void Promote_Uint16MulWidensToUint32()
    {
        // uint16 * int promotes to uint32, so a result that overflows 16 bits is kept intact.
        // This is the LoadIntoReg widening path: a uint16 source must zero-extend its real high
        // byte into the uint32 destination (the bug gave 88 instead of the wide value).
        const string body =
            "from pymcu.types import uint16, uint32\n\n" +
            "def run(s: uint8):\n" +
            "    a: uint16 = 60000\n" +
            "    a = a + s\n" +                        // 60005 (fits uint16)
            "    big: uint32 = a * 2\n" +              // 120010 -> needs uint32
            "    print(big)\n";
        RunSeed(body, 5, 1).Should().Equal(120010);
    }
}
