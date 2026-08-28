using FluentAssertions;
using NUnit.Framework;

namespace PyMCU.IntegrationTests.Tests.AVR;

/// <summary>
/// Integration tests for fixtures/float-local-overload (PyMCU#214, upstream of PyMCU#182).
///
/// `InferExprType` resolved a name under one qualification only, so a plain function local was
/// never found and every float local inferred UINT8. `math.floor(x)` on a float local therefore
/// selected the INTEGER overload, and an integer floor over a value it read as zero returned
/// zero: three different inputs, one answer, live and silent on main.
///
/// The seed is written HERE, after Reset and before the CPU runs. A literal folds and would
/// measure the constant folder instead of the call. qemu does not retain a write to GPIOR0.
/// </summary>
[TestFixture]
public class FloatLocalOverloadTests
{
    private const int Gpior0Addr = 0x3E;
    private const int Gpior1Addr = 0x4A;

    private static SimSession _session = null!;

    [OneTimeSetUp]
    public void BuildFirmware() => _session = new SimSession(PymcuCompiler.BuildFixture("float-local-overload"));

    private int Run(byte seed)
    {
        var uno = _session.Reset();
        uno.Data[Gpior0Addr] = seed;
        uno.RunToBreak();
        return uno.Data[Gpior1Addr];
    }

    // seed, x = seed - 1.5, then floor(x) + 100. The right column is what CPython gives.
    // Seed 2 is kept even though it cannot fail: there the buggy answer and the correct one are
    // both 100, so a fixture built on that seed alone would have been green over the defect.
    [TestCase((byte)0,  98)]   // x = -1.5, floor -2
    [TestCase((byte)1,  99)]   // x = -0.5, floor -1
    [TestCase((byte)2, 100)]   // x =  0.5, floor  0   <- indistinguishable from the defect
    [TestCase((byte)3, 101)]   // x =  1.5, floor  1
    public void FloorOfAFloatLocalMatchesCPython(byte seed, int expected)
    {
        Run(seed).Should().Be(expected,
            "math.floor of a float local must read the local, not an integer overload's zero");
    }

    /// <summary>
    /// The defect's signature was one answer for every input. Varying the seed is what separates
    /// a correct run from a firmware that never read the operand, and it is also what catches
    /// the simulator dropping the GPIOR write, in which case every assertion above would pass
    /// vacuously. EVERY SUITE THAT SEEDS NEEDS ONE OF THESE.
    /// </summary>
    [Test]
    public void DifferentInputsGiveDifferentAnswers()
    {
        Run(0).Should().NotBe(Run(3),
            "three different inputs producing one answer is the signature of an operand never read");
    }
}
