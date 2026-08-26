using Avr8Sharp.TestKit;
using Avr8Sharp.TestKit.Boards;
using FluentAssertions;
using NUnit.Framework;

namespace PyMCU.IntegrationTests.Tests.AVR;

/// <summary>
/// Integration tests for fixtures/math-rounding (PyMCU#174).
///
/// `math.floor`, `math.ceil` and `math.trunc` did not exist; asking for one produced
/// "call to undefined function 'math_floor'", naming the compiler's internal symbol.
/// They are implemented in `pymcu/math/__init__.py` on top of `int32(x)`, whose truncation
/// is toward zero, adjusting by one only when x had a fractional part.
///
/// The value is seeded through GPIOR0 rather than written as a literal: a literal folds, so
/// the test would measure the constant folder rather than the functions.
///
///     x = (seed - 4) / 2      seed 0..8  ->  -2.0 .. 2.0 in halves
///
/// Every one of the nine values is checked against what CPython returns for the same input,
/// which is the point: floor and ceil differ from trunc only on the negative fractions, and
/// those are exactly the rows a naive implementation gets wrong.
///
/// Results come back four bits each, since every answer is in -3..3, so they are
/// sign-extended here. Data-space: GPIOR0 = 0x3E, GPIOR1 = 0x4A, GPIOR2 = 0x4B.
/// </summary>
[TestFixture]
public class MathRoundingTests
{
    private const int Gpior0Addr = 0x3E;
    private const int Gpior1Addr = 0x4A;
    private const int Gpior2Addr = 0x4B;

    private static SimSession _session = null!;

    [OneTimeSetUp]
    public void BuildFirmware() => _session = new SimSession(PymcuCompiler.BuildFixture("math-rounding"));

    /// <summary>Sign-extends a four-bit two's complement result.</summary>
    private static int Nibble(int v) => (v & 0x0F) > 7 ? (v & 0x0F) - 16 : (v & 0x0F);

    private static (int Floor, int Ceil, int Trunc) Round(byte seed)
    {
        var uno = _session.Reset();
        uno.Data[Gpior0Addr] = seed;
        uno.RunToBreak();

        var packed = uno.Data[Gpior1Addr];
        return (Nibble(packed), Nibble(packed >> 4), Nibble(uno.Data[Gpior2Addr]));
    }

    // seed, x, floor, ceil, trunc -- the right column is what CPython returns for the same x.
    private static readonly object[] Cases =
    {
        new object[] { (byte)0, -2.0, -2, -2, -2 },
        new object[] { (byte)1, -1.5, -2, -1, -1 },
        new object[] { (byte)2, -1.0, -1, -1, -1 },
        new object[] { (byte)3, -0.5, -1,  0,  0 },
        new object[] { (byte)4,  0.0,  0,  0,  0 },
        new object[] { (byte)5,  0.5,  0,  1,  0 },
        new object[] { (byte)6,  1.0,  1,  1,  1 },
        new object[] { (byte)7,  1.5,  1,  2,  1 },
        new object[] { (byte)8,  2.0,  2,  2,  2 },
    };

    [TestCaseSource(nameof(Cases))]
    public void FloorMatchesCPython(byte seed, double x, int floor, int ceil, int trunc)
        => Round(seed).Floor.Should().Be(floor, $"floor({x})");

    [TestCaseSource(nameof(Cases))]
    public void CeilMatchesCPython(byte seed, double x, int floor, int ceil, int trunc)
        => Round(seed).Ceil.Should().Be(ceil, $"ceil({x})");

    [TestCaseSource(nameof(Cases))]
    public void TruncMatchesCPython(byte seed, double x, int floor, int ceil, int trunc)
        => Round(seed).Trunc.Should().Be(trunc, $"trunc({x})");

    [Test]
    public void TheThreeDisagreeOnANegativeFraction()
    {
        // The row that makes the sweep worth running: at -1.5 a correct floor, ceil and trunc
        // are three different numbers. An implementation that returned the truncation for all
        // three would pass every other case here.
        var r = Round(1);
        r.Floor.Should().Be(-2);
        r.Ceil.Should().Be(-1);
        r.Trunc.Should().Be(-1);
        r.Floor.Should().NotBe(r.Ceil);
    }
}
