using Avr8Sharp.TestKit.Boards;
using FluentAssertions;
using NUnit.Framework;

namespace PyMCU.IntegrationTests.Tests.AVR;

/// <summary>
/// Integration tests for fixtures/buffer-grown-by-extend.
///
/// PyMCU#362. A module-level scratch buffer grown by `.extend()` at construction is how every
/// CircuitPython register driver sizes its bus buffer, and `.extend()` had no dispatch for a
/// bytearray at all: the call fell through to the message written for an untyped list.
///
/// The buffer lives in another module on purpose. Keyed off the lowered variable's name this
/// worked in a single file and in no library, because a buffer declared in an imported module
/// is registered under the spelling that module wrote while the value it lowers to carries the
/// qualified one.
///
/// The assertions are on the SIZE the buffer ends up with and on the bytes it holds. A test
/// that only checked the program builds would pass with a one-byte buffer, and every store past
/// the first would land on whatever followed it in SRAM.
/// </summary>
[TestFixture]
public class BufferGrownByExtendTests
{
    private static SimSession _session = null!;

    [OneTimeSetUp]
    public void BuildFirmware() =>
        _session = new SimSession(PymcuCompiler.BuildFixture("buffer-grown-by-extend"));

    private ArduinoUnoSimulation Boot()
    {
        var uno = _session.Reset();
        uno.RunUntilSerial(uno.Serial, "done\n", maxMs: 1000);
        return uno;
    }

    [Test]
    public void TheBufferTakesTheLargestSizeAskedFor()
    {
        Boot().Serial.Text.Should().StartWith("5\n",
            "_fit(2), _fit(4) and _fit(1) ask for 3, 5 and 2 bytes; the buffer is the largest");
    }

    [Test]
    public void AByteInTheGrownRegion_HoldsWhatWasStored()
    {
        Boot().Serial.Text.Should().Contain("85\n",
            "index 4 exists only because the buffer grew, and must be real storage");
    }

    [Test]
    public void AReaderInAnotherModule_SeesTheSameBuffer()
    {
        Boot().Serial.Text.Should().Contain("51\n",
            "b.read() reads index 2 of the same buffer the caller wrote");
    }
}
