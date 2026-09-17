using Avr8Sharp.TestKit.Boards;
using FluentAssertions;
using NUnit.Framework;

namespace PyMCU.IntegrationTests.Tests.AVR;

/// <summary>
/// Integration tests for fixtures/enumerate-buffer-param.
///
/// Two enumerate() shapes the Adafruit drivers need. The first is
/// adafruit_tcs34725's: a class-level `_BUFFER = bytearray(8)` passed as
/// `i2c.write(self._BUFFER)` through an inlined wrapper into an @inline callee
/// whose `for i, b in enumerate(buffer)` must resolve the alias to the storage
/// filed under the module init function (`main.Sensor__BUFFER`), and whose
/// `buffer[i] = v` stores must land on the same storage. The second is
/// adafruit_bmp280's: `bytes([register & 0xFF])` with a run-time element has no
/// constant sequence to bind, so the argument materializes a hidden buffer that
/// enumerate() iterates -- carrying the evaluated value, not a zero.
///
/// Every expectation is CPython's answer for the same program.
/// </summary>
[TestFixture]
public class EnumerateBufferParamTests
{
    private static SimSession _session = null!;

    [OneTimeSetUp]
    public void BuildFirmware() =>
        _session = new SimSession(PymcuCompiler.BuildFixture("enumerate-buffer-param"));

    private ArduinoUnoSimulation Boot()
    {
        var uno = _session.Reset();
        uno.RunUntilSerial(uno.Serial, "done\n", maxMs: 1000);
        return uno;
    }

    [Test]
    public void Enumerate_ClassAttributeBuffer_ReadsItsStorage()
    {
        Boot().Serial.Text.Should().Contain("42\n11\n",
            "enumerate(self._BUFFER) through write(buf) must read the class attribute's bytes");
    }

    [Test]
    public void Enumerate_AliasedBuffer_StoresThroughTheAlias()
    {
        Boot().Serial.Text.Should().Contain("0\n7\n",
            "buffer[i] = i * 7 inside enumerate() must store into the caller's _BUFFER");
    }

    [Test]
    public void Enumerate_RuntimeBytesArg_IteratesTheMaterializedBuffer()
    {
        Boot().Serial.Text.Should().Contain("1\n",
            "bytes([self._BUFFER[0] + 1]) with a run-time element sends the evaluated byte (0 + 1)");
    }
}

/// <summary>
/// The same fixture under the Python-frontend parser.
/// </summary>
[TestFixture]
public class EnumerateBufferParamPyParserTests
{
    private static SimSession _session = null!;

    [OneTimeSetUp]
    public void BuildFirmware() =>
        _session = new SimSession(PymcuCompiler.BuildFixturePyParser("enumerate-buffer-param"));

    [Test]
    public void AllShapes_ProduceTheExpectedStream()
    {
        var uno = _session.Reset();
        uno.RunUntilSerial(uno.Serial, "done\n", maxMs: 1000);
        uno.Serial.Text.Should().Contain("42\n11\n0\n7\n1\n");
    }
}
