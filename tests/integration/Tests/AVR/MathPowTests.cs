using Avr8Sharp.TestKit.Boards;
using FluentAssertions;
using NUnit.Framework;

namespace PyMCU.IntegrationTests.Tests.AVR;

/// <summary>
/// Integration tests for fixtures/math-pow: runtime `math.pow` on the software
/// float (the exponents adafruit_bmp280 altitude and adafruit_tcs34725 gamma
/// correction need -- `pow(p / sea_level, 0.1903)` can never be a compile-time
/// integer exponentiation).
///
/// The stdlib implements it as 2 ** (y * log2(x)): atanh series for log2 after
/// range-reducing into [1, 2), Taylor exp for the fractional power of two, and
/// exact doubling for the integer part.
/// </summary>
[TestFixture]
public class MathPowTests
{
    private SimSession _session = null!;

    [OneTimeSetUp]
    public void BuildFirmware() => _session = new SimSession(PymcuCompiler.BuildFixture("math-pow"));

    private string Transcript()
    {
        var uno = _session.Reset();
        uno.RunUntilSerial(uno.Serial, "END\n", maxMs: 800);
        return uno.Serial.Text;
    }

    [Test]
    public void Pow_IntegerExponentIsExact()
        => Transcript().Should().Contain("A 1024\n",
            "pow(2.0, 10.0) is exactly 1024 -- the integer part is exact doubling");

    [Test]
    public void Pow_FractionalBaseAndIntegerExponent()
        => Transcript().Should().Contain("B 225\n",
            "pow(1.5, 2.0) is 2.25");

    [Test]
    public void Pow_FractionalExponentIsSquareRoot()
        => Transcript().Should().Contain("C 3162\n",
            "pow(10.0, 0.5) is sqrt(10) ~= 3.162");

    [Test]
    public void Pow_NegativeExponentReciprocates()
        => Transcript().Should().Contain("D 200\n",
            "pow(0.5, -1.0) is 2.0");

    [Test]
    public void Pow_SmallExponentNearOne_Bmp280AltitudeShape()
        => Transcript().Should().Contain("E 1000\n",
            "pow(1.001, 0.1903) ~= 1.00019 -- the altitude formula's exponent");

    [Test]
    public void Pow_DomainEdges()
        => Transcript().Should().Contain("F 0\nG 100\n",
            "pow(0.0, 5.0) is 0 and pow(x, 0.0) is 1.0");

    [Test]
    public void BarePow_RuntimeOperandsLowerToTheSameHelper()
        => Transcript().Should().Contain("H 1414\n",
            "pow(2.0, 0.5) is sqrt(2) ~= 1.414 -- the bare builtin, no `import math` " +
            "needed, the spelling adafruit_tcs34725's gamma table uses");

    [Test]
    public void BarePow_ConstantIntegersStillFold()
        => Transcript().Should().Contain("I 8\n",
            "pow(2, 3) folds to the constant 8 -- no float code pulled in");
}
