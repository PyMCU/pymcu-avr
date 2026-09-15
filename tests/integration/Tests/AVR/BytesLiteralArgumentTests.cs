using Avr8Sharp.TestKit;
using FluentAssertions;
using NUnit.Framework;

namespace PyMCU.IntegrationTests.Tests.AVR;

/// <summary>
/// fixtures/bytes-literal-arg (PyMCU#431): `bytes([...])` / `bytes(N)` written as a
/// call argument.
///
/// `bytearray(...)` written the same way already had two recognizers -- one that normalises
/// it to a `ListExpr` for an `@inline` callee's UNANNOTATED parameter (so `for x in param`
/// unrolls: adafruit_bus_device's `bus_device.write(bytes([A_DEVICE_REGISTER]))`, where
/// `write` is `for i, b in enumerate(buffer): ...`), and one that materialises a hidden fixed
/// buffer for a REGULAR callee's `bytearray`/`bytes` parameter. Neither looked for the callee
/// name `bytes`, only `bytearray`, so `bytes([...])` fell through both and reached the generic
/// call-expression visitor, which reported "bytes() is a Python builtin that PyMCU does not
/// provide" -- false, since a `bytes` PARAMETER is already read as the exact buffer a
/// `bytearray` parameter is.
/// </summary>
[TestFixture]
public class BytesLiteralArgumentTests
{
    private static SimSession _session = null!;

    [OneTimeSetUp]
    public void BuildFirmware()
    {
        _session = new SimSession(PymcuCompiler.BuildFixture("bytes-literal-arg"));
    }

    private static string Output()
    {
        var uno = _session.Reset();
        uno.RunMilliseconds(400);
        return uno.Serial.Text;
    }

    [Test]
    public void ABytesListLiteralUnrollsIntoAnInlineCallee()
        => Output().Should().Contain("inline_lit 18", "5 + 6 + 7, unrolled at the call site");

    [Test]
    public void ABytesConstructorSizeArgumentUnrollsAsThatManyZeroesIntoAnInlineCallee()
        => Output().Should().Contain("inline_zero 0", "bytes(3) is three zero bytes, not three garbage ones");

    [Test]
    public void ABytesListLiteralArgumentReachesARegularCalleesBytesParameter()
        => Output().Should().Contain("outline_first 5");

    [Test]
    public void TheSameLiteralAnswersEveryElementOfARegularCallee()
        => Output().Should().Contain("outline_third 7", "buf[2] is the third byte of the SAME literal, not a fresh one");

    [Test]
    public void ABytesLocalVariableIsTheSameFixedBufferABytearrayLocalIs()
        => Output().Should().Contain("local_lit 6", "1 + 2 + 3");

    [Test]
    public void TheProgramRunsToItsEnd() => Output().Should().Contain("done");
}
