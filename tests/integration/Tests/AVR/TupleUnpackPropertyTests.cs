using Avr8Sharp.TestKit.Boards;
using FluentAssertions;
using NUnit.Framework;

namespace PyMCU.IntegrationTests.Tests.AVR;

/// <summary>
/// Integration tests for fixtures/tuple-unpack-property.
///
/// `a, b = f()` unpacked inline multi-return calls, but `a, b = obj.prop` --
/// adafruit_tcs34725's `r, g, b, clear = self.color_raw` -- refused because a
/// MemberAccessExpr never reached the lastTupleResults path, even though the
/// getter expands inline and returns a tuple literal like any inlined call.
/// The fixture also covers a getter that itself unpacks a method's tuple
/// (color_raw's shape) and a plain non-@inline method call.
///
/// Every expectation is CPython's answer for the same program.
/// </summary>
[TestFixture]
public class TupleUnpackPropertyTests
{
    private static SimSession _session = null!;

    [OneTimeSetUp]
    public void BuildFirmware() =>
        _session = new SimSession(PymcuCompiler.BuildFixture("tuple-unpack-property"));

    private ArduinoUnoSimulation Boot()
    {
        var uno = _session.Reset();
        uno.RunUntilSerial(uno.Serial, "done\n", maxMs: 1000);
        return uno;
    }

    [Test]
    public void PropertyUnpack_BindsEachElement()
    {
        Boot().Serial.Text.Should().Contain("3\n4\n",
            "a, b = s.pair must bind the getter's returned 2-tuple");
    }

    [Test]
    public void NestedUnpack_GetterUnpacksAMethodCall()
    {
        Boot().Serial.Text.Should().Contain("20\n30\n40\n10\n",
            "r, g, b, c = s.raw must unpack the tuple the getter built from read4(0) = (10,20,30,40), reordered");
    }

    [Test]
    public void MethodCallUnpack_WithARuntimeArgument()
    {
        Boot().Serial.Text.Should().Contain("6\n16\n",
            "w, x, y, z = s.read4(-4) must bind (-4+10, -4+20, ...) = (6, 16, 26, 36)");
    }
}

/// <summary>
/// The same fixture under the Python-frontend parser.
/// </summary>
[TestFixture]
public class TupleUnpackPropertyPyParserTests
{
    private static SimSession _session = null!;

    [OneTimeSetUp]
    public void BuildFirmware() =>
        _session = new SimSession(PymcuCompiler.BuildFixturePyParser("tuple-unpack-property"));

    [Test]
    public void AllShapes_ProduceTheExpectedStream()
    {
        var uno = _session.Reset();
        uno.RunUntilSerial(uno.Serial, "done\n", maxMs: 1000);
        uno.Serial.Text.Should().Contain("3\n4\n20\n30\n40\n10\n6\n16\n");
    }
}
