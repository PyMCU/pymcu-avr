using Avr8Sharp.TestKit.Boards;
using FluentAssertions;
using NUnit.Framework;

namespace PyMCU.IntegrationTests.Tests.AVR;

/// <summary>
/// Integration tests for fixtures/float-modulo (pymcu-avr#9).
///
/// `%` between two floats had no lowering and aborted the build with "Float comparison op Mod
/// not supported". It is now fmodf plus one correction, because Python's float `%` is floored
/// and fmodf is truncated.
///
/// The four sign combinations are separate tests on purpose: a truncating implementation gets
/// two of the four right, so a fixture that only checked positive operands would pass over
/// the whole defect.
/// </summary>
[TestFixture]
public class FloatModuloTests
{
    private static SimSession _session = null!;

    [OneTimeSetUp]
    public void BuildFirmware() => _session = new SimSession(PymcuCompiler.BuildFixture("float-modulo"));

    private string Boot()
    {
        var uno = _session.Reset();
        uno.RunUntilSerial(uno.Serial, "done\n", maxMs: 4000);
        return uno.Serial.Text;
    }

    [Test]
    public void BothOperandsPositive()
    {
        Boot().Should().StartWith("1.5\n", "3.5 % 2.0 is 1.5, where fmodf already agrees");
    }

    [Test]
    public void ANegativeDividendTakesTheSignOfTheDivisor()
    {
        Boot().Should().Contain("1.5\n0.5\n",
            "-3.5 % 2.0 is 0.5 in Python; fmodf on its own gives -1.5");
    }

    [Test]
    public void ANegativeDivisorMakesTheResultNegative()
    {
        Boot().Should().Contain("0.5\n-0.5\n-1.5\n",
            "3.5 % -2.0 is -0.5 and -3.5 % -2.0 is -1.5");
    }

    [Test]
    public void AnIntegerLiteralDivisorReachesTheSamePath()
    {
        Boot().Should().Contain("1.5\n0.5\n0.5\n",
            "p % 2, p % 1 and n % 1 stopped being rewritten into a mask by PyMCU#128");
    }

    [Test]
    public void AValueModuloItselfIsZero()
    {
        Boot().Should().Contain("0.0\n0.5\n");
    }

    [Test]
    public void TheResultIsExactWhereTheArithmeticIdentityIsNot()
    {
        Boot().Should().Contain("1.0\ndone\n",
            "1e9 % 3.0 is 1.0; computing it as x - floor(x / y) * y gives 0.0 in float32");
    }
}
