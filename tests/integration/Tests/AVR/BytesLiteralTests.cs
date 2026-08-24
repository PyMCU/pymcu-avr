using FluentAssertions;
using NUnit.Framework;
using Avr8Sharp.TestKit.Boards;

namespace PyMCU.IntegrationTests.Tests.AVR;

/// <summary>
/// Integration tests for fixtures/bytes-literal (PyMCU#55).
///
/// A b"..." literal is how protocol constants are written on an MCU, and it failed with
/// "IR Generation: Unknown Expression type: ListExpr" -- a phase name plus an AST class name.
///
/// The bytes are read back from the registers rather than asserted by size, because a buffer
/// laid out with the wrong contents compiles just as small as one with the right contents.
/// </summary>
[TestFixture]
public class BytesLiteralTests
{
    private const int Gpior0Addr = 0x3E;
    private const int Gpior1Addr = 0x4A;
    private const int Gpior2Addr = 0x4B;

    private SimSession _session = null!;

    [OneTimeSetUp]
    public void BuildFirmware() => _session = new SimSession(PymcuCompiler.BuildFixture("bytes-literal"));

    private ArduinoUnoSimulation Boot()
    {
        var uno = _session.Reset();
        uno.RunToBreak();
        return uno;
    }

    [Test]
    public void BytesLiteral_KeepsItsBytes()
    {
        Boot().Data[Gpior0Addr].Should().Be(0x02, "frame[1] of b\"\\x01\\x02\\x03\"");
    }

    [Test]
    public void BytearrayFromALiteral_TakesItsSizeAndContents()
    {
        Boot().Data[Gpior1Addr].Should().Be(0x41, "mutable[0] of b\"AB\" is 'A'");
    }

    [Test]
    public void ThatBufferIsStillWritable()
    {
        Boot().Data[Gpior2Addr].Should().Be(0x7F, "mutable[1] was stored into");
    }
}
