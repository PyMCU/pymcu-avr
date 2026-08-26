using FluentAssertions;
using NUnit.Framework;
using Avr8Sharp.TestKit.Boards;

namespace PyMCU.IntegrationTests.Tests.AVR;

/// <summary>
/// An 8-bit register read in a wider context (pymcu-avr#13).
///
/// The backend loaded two bytes from an 8-bit register's address whenever the destination was
/// wider, so the register one byte above arrived as the high half: `d: uint16 = GPIOR1.value`
/// read GPIOR2 into R25, and the same shape on GPIOR0 read SREG. The handoff MIR was correct
/// throughout, asking for a one-byte read widened into a wider destination.
///
/// GPIOR2, the neighbour, is seeded to 0xFF on every run. That is the assertion's teeth: with
/// the neighbour at 0 the broken backend returns the right number and every check here passes
/// against a compiler that is still wrong.
///
/// Seed 200 is not a duplicate of seed 60. It pushes `100 + x` and `x * 2` over 255, so the
/// high byte must come back as 1, which fails a "fix" that just clears the high byte.
/// </summary>
[TestFixture]
public class RegisterWidthWideningTests
{
    private const int Gpior0Addr = 0x3E;   // where the program leaves each answer
    private const int Gpior1Addr = 0x4A;   // the 8-bit source
    private const int Gpior2Addr = 0x4B;   // its neighbour, one byte above

    private SimSession _session = null!;

    [OneTimeSetUp]
    public void BuildFirmware() =>
        _session = new SimSession(PymcuCompiler.BuildFixture("register-width-widening"));

    private byte Checkpoint(int n, byte seed)
    {
        var uno = _session.Reset();
        uno.Data[Gpior1Addr] = seed;
        uno.Data[Gpior2Addr] = 0xFF;
        for (int i = 0; i < n; i++)
        {
            if (i > 0) uno.RunInstructions(1);
            uno.RunToBreak();
        }
        return uno.Data[Gpior0Addr];
    }

    [TestCase((byte)60)]
    [TestCase((byte)200)]
    public void WidenedRegister_HasNoNeighbourInItsHighByte(byte seed)
        => Checkpoint(1, seed).Should().Be(0,
            "the high byte of a widened 8-bit register is zero, not GPIOR2");

    [TestCase((byte)60)]
    [TestCase((byte)200)]
    public void WidenedRegister_KeepsItsOwnValueInTheLowByte(byte seed)
        => Checkpoint(2, seed).Should().Be(seed);

    // 100 + 60 = 160 (high 0), 100 + 200 = 300 (high 1). Both directions.
    [TestCase((byte)60, (byte)0)]
    [TestCase((byte)200, (byte)1)]
    public void RegisterAsAnOperandOfWiderArithmetic_CarriesTheRealHighByte(byte seed, byte expected)
        => Checkpoint(3, seed).Should().Be(expected,
            "100 + seed is a real 16-bit sum, so the high byte is neither the neighbour nor always zero");

    // 60 * 2 = 120 (high 0), 200 * 2 = 400 (high 1).
    [TestCase((byte)60, (byte)0)]
    [TestCase((byte)200, (byte)1)]
    public void RegisterMultipliedIntoSixteenBits_CarriesTheRealHighByte(byte seed, byte expected)
        => Checkpoint(4, seed).Should().Be(expected);

    [TestCase((byte)60)]
    [TestCase((byte)200)]
    public void WidenedToThirtyTwoBits_HasNoNeighbourInByteOne(byte seed)
        => Checkpoint(5, seed).Should().Be(0,
            "the 32-bit case loaded two bytes and zero-extended the rest, so byte 1 was the neighbour");
}
