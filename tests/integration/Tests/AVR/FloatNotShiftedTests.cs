using Avr8Sharp.TestKit.Boards;
using FluentAssertions;
using NUnit.Framework;

namespace PyMCU.IntegrationTests.Tests.AVR;

/// <summary>
/// Integration tests for fixtures/float-not-shifted (pymcu-avr#5, PyMCU#128).
///
/// The IR optimizer's power-of-two rewrites were applied without checking that the operands
/// were integers. A float multiplied by 2 reached this backend as a float LShift and aborted
/// codegen; a float floor-divided by 1 was rewritten to the float itself and printed 3.5
/// where CPython gives 3.0, with no diagnostic at all.
///
/// Half of this fixture is there to prove the guard did not go too far: the identities on
/// floats and the power-of-two rewrites on integers both have to keep working.
/// </summary>
[TestFixture]
public class FloatNotShiftedTests
{
    private static SimSession _session = null!;

    [OneTimeSetUp]
    public void BuildFirmware() => _session = new SimSession(PymcuCompiler.BuildFixture("float-not-shifted"));

    private string Boot()
    {
        var uno = _session.Reset();
        uno.RunUntilSerial(uno.Serial, "done\n", maxMs: 4000);
        return uno.Serial.Text;
    }

    [Test]
    public void AFloatTimesAPowerOfTwoCompilesAndMultiplies()
    {
        Boot().Should().StartWith("3.5\n7.0\n28.0\n",
            "these used to abort codegen with 'Float comparison op LShift not supported'");
    }

    [Test]
    public void AFloatFlooredByAPowerOfTwoIsADivision()
    {
        Boot().Should().Contain("1.0\n3.0\n",
            "3.5 // 2 is 1.0, and 3.5 // 1 is 3.0 rather than 3.5, which is the silent half");
    }

    [Test]
    public void TheIdentitiesOnFloatsStillHold()
    {
        Boot().Should().Contain("3.5\n0.0\n3.5\n3.5\n10.5\n",
            "x * 1, x * 0, x + 0 and x - 0 share the routine and must survive the guard");
    }

    [Test]
    public void ANegativeFloatFloorsTowardMinusInfinity()
    {
        Boot().Should().Contain("-3.5\n-7.0\n-2.0\n-4.0\n",
            "a shift could never produce -2.0 from -3.5 // 2");
    }

    [Test]
    public void TheFoldedFormWithThePowerOfTwoOnTheOtherSideCompiles()
    {
        Boot().Should().Contain("1536.0\n1500.0\n",
            "float(512) * 3.0 aborted while float(500) * 3.0 compiled; only c differs");
    }

    [Test]
    public void TheIntegerRewritesAreUntouched()
    {
        Boot().Should().Contain("968\n3872\n121\n8\n",
            "the guard must not disable the strength reduction it exists to protect");
    }
}
