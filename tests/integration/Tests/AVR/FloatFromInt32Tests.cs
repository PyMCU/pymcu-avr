using Avr8Sharp.TestKit.Boards;
using FluentAssertions;
using NUnit.Framework;

namespace PyMCU.IntegrationTests.Tests.AVR;

/// <summary>
/// Integration tests for fixtures/float-from-int32 (pymcu-avr#7).
///
/// Converting a 32-bit integer to float read only its low 16 bits: float(100000) was 34464.0.
/// The integer printed correctly the whole time, which is what made it look like a printing
/// problem rather than a conversion one, so the fixture prints the integer first.
/// </summary>
[TestFixture]
public class FloatFromInt32Tests
{
    private static SimSession _session = null!;

    [OneTimeSetUp]
    public void BuildFirmware() => _session = new SimSession(PymcuCompiler.BuildFixture("float-from-int32"));

    private string Boot()
    {
        var uno = _session.Reset();
        uno.RunUntilSerial(uno.Serial, "done\n", maxMs: 2000);
        return uno.Serial.Text;
    }

    [Test]
    public void TheIntegerItselfWasNeverWrong()
    {
        Boot().Should().StartWith("100000\n");
    }

    [Test]
    public void AllFourBytesReachTheConversion()
    {
        Boot().Should().Contain("100000\n100000.0\n", "the top half used to be cleared away");
    }

    [Test]
    public void ANegativeInt32KeepsItsSign()
    {
        Boot().Should().Contain("-100000.0\n");
    }

    [Test]
    public void AUint32AboveTwoToThe31IsNotNegative()
    {
        Boot().Should().Contain("positive\n",
            "an unsigned source needs the unsigned helper; the signed one makes it negative. "
            + "The value is compared rather than printed because print() saturates every float "
            + "above 21474836.48 (PyMCU#99)");
    }
}
