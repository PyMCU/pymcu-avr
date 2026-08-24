using FluentAssertions;
using NUnit.Framework;
using Avr8Sharp.TestKit.Boards;

namespace PyMCU.IntegrationTests.Tests.AVR;

/// <summary>
/// Integration tests for fixtures/class-param-function.
///
/// A free function that takes a class instance (`def blink_twice(led: Pin)`) has no
/// subroutine ABI: the instance fields live in the caller's frame, so the function is
/// expanded at the call site, the way an explicit @inline of the same shape already was.
///
/// Both halves of the hole this covers produced a wrong program, in opposite ways:
///   PyMCU#71 -- a class in the first parameter was kept only for on-demand ISR
///               synthesis and never emitted; the linker reported `undefined reference`.
///   PyMCU#72 -- a class in any other position was lowered as an ordinary function whose
///               field reads were never bound; the program linked and read zero for the
///               field (measured: 1 instead of 8).
///
/// The instance field is seeded from GPIOR0, which this fixture writes before running, so
/// the constant folder cannot resolve the program at compile time and leave a false green.
///
/// Checkpoints (ATmega328P data-space), with GPIOR0 = 7:
///   GPIOR1 (0x4A) = leer(c)                    = 7
///   GPIOR2 (0x4B) = leer_k(2, c) after poner() = 12
/// </summary>
[TestFixture]
public class ClassParamFunctionTests
{
    private const int Gpior0Addr = 0x3E;
    private const int Gpior1Addr = 0x4A;
    private const int Gpior2Addr = 0x4B;

    private SimSession _session = null!;

    [OneTimeSetUp]
    public void BuildFirmware() => _session = new SimSession(PymcuCompiler.BuildFixture("class-param-function"));

    private ArduinoUnoSimulation Boot()
    {
        var uno = _session.Reset();
        uno.Data[Gpior0Addr] = 7;   // volatile seed, read at run time
        return uno;
    }

    [Test]
    public void Instance_AsOnlyParameter_IsRead()
    {
        var uno = Boot();
        uno.RunToBreak();
        uno.Data[Gpior1Addr].Should().Be(7,
            "leer(c) should return the field seeded from GPIOR0");
    }

    [Test]
    public void Instance_AfterANumericParameter_SeesTheMutatedField()
    {
        var uno = Boot();
        uno.RunToBreak();
        uno.Data[Gpior2Addr].Should().Be(12,
            "poner(c, 10) should mutate the field through a free function, "
            + "and leer_k(2, c) should read 10 back and add 2");
    }
}
