using Avr8Sharp.TestKit.Boards;
using FluentAssertions;
using NUnit.Framework;

namespace PyMCU.IntegrationTests.Tests.AVR;

/// <summary>
/// Integration tests for examples/avr/tuple-index.
/// Exercises `f()[k]` on an inline multi-return call: the subscript picks the
/// k-th result slot the expansion wrote, so the tuple is never materialised.
///   - annotated `-> (uint8, uint8)` callee
///   - unannotated callee, arity derived from its returns
///   - method call `self._method()[k]` (the adafruit_tcs34725 shape)
///   - constructor + __getitem__ subscript preserved
///   - fixed-buffer-returning call indexed
/// </summary>
[TestFixture]
public class TupleIndexTests
{
    private static SimSession _session = null!;

    [OneTimeSetUp]
    public void BuildFirmware() => _session = new SimSession(PymcuCompiler.BuildFixture("tuple-index"));

    private ArduinoUnoSimulation Boot()
    {
        var uno = _session.Reset();
        uno.RunUntilSerial(uno.Serial, "TI\n", maxMs: 200);
        return uno;
    }

    [Test]
    public void Boot_SendsBanner() =>
        Boot().Serial.Text.Should().Contain("TI");

    [Test]
    public void AnnotatedTuple_Index0_ReadsFirstSlot()
    {
        // pair(10)[0] = 11 = 0x0B → A:0B
        var uno = Boot();
        uno.RunUntilSerial(uno.Serial, s => s.Contains("B:0C\n"), maxMs: 300);
        uno.Serial.Text.Should().Contain("A:0B", "pair(10)[0] should be 11");
    }

    [Test]
    public void AnnotatedTuple_Index1_ReadsSecondSlot()
    {
        // pair(10)[1] = 12 = 0x0C → B:0C
        var uno = Boot();
        uno.RunUntilSerial(uno.Serial, s => s.Contains("B:0C\n"), maxMs: 300);
        uno.Serial.Text.Should().Contain("B:0C", "pair(10)[1] should be 12");
    }

    [Test]
    public void UnannotatedTuple_BranchA_Index2()
    {
        // tri(20)[2] = 3 → C:03
        var uno = Boot();
        uno.RunUntilSerial(uno.Serial, s => s.Contains("C:03\n"), maxMs: 300);
        uno.Serial.Text.Should().Contain("C:03", "tri(20)[2] should be 3");
    }

    [Test]
    public void UnannotatedTuple_BranchB_Indices()
    {
        // tri(5)[0] = 5 → D:05 ; tri(5)[2] = 7 → E:07
        var uno = Boot();
        uno.RunUntilSerial(uno.Serial, s => s.Contains("E:07\n"), maxMs: 300);
        uno.Serial.Text.Should().Contain("D:05", "tri(5)[0] should be 5");
        uno.Serial.Text.Should().Contain("E:07", "tri(5)[2] should be 7");
    }

    [Test]
    public void MethodTuple_Indices()
    {
        // sensor.read()[0] = 42 = 0x2A → F:2A ; [1] = 43 = 0x2B → G:2B
        var uno = Boot();
        uno.RunUntilSerial(uno.Serial, s => s.Contains("G:2B\n"), maxMs: 300);
        uno.Serial.Text.Should().Contain("F:2A", "sensor.read()[0] should be 42");
        uno.Serial.Text.Should().Contain("G:2B", "sensor.read()[1] should be 43");
    }

    [Test]
    public void CtorGetItem_Preserved()
    {
        // Vec(3, 4)[1] = 4 → H:04
        var uno = Boot();
        uno.RunUntilSerial(uno.Serial, s => s.Contains("H:04\n"), maxMs: 300);
        uno.Serial.Text.Should().Contain("H:04", "Vec(3,4)[1] should read through __getitem__");
    }

    [Test]
    public void BufferReturn_Indices()
    {
        // fetch()[1] = 9 → I:09 ; fetch()[2] = 10 = 0x0A → J:0A
        var uno = Boot();
        uno.RunUntilSerial(uno.Serial, s => s.Contains("J:0A\n"), maxMs: 300);
        uno.Serial.Text.Should().Contain("I:09", "fetch()[1] should be 9");
        uno.Serial.Text.Should().Contain("J:0A", "fetch()[2] should be 10");
    }

    [Test]
    public void BoundTuple_PrintsAsTuple()
    {
        // t = pair(11); print(t) → K=(12, 13)  -- CPython's tuple text
        var uno = Boot();
        uno.RunUntilSerial(uno.Serial, s => s.Contains("K=(12, 13)\n"), maxMs: 300);
        uno.Serial.Text.Should().Contain("K=(12, 13)", "print(t) should write the tuple repr");
    }

    [Test]
    public void BoundTuple_IndexReadsSlot()
    {
        // t[0] = 12 = 0x0C → L:0C
        var uno = Boot();
        uno.RunUntilSerial(uno.Serial, s => s.Contains("L:0C\n"), maxMs: 300);
        uno.Serial.Text.Should().Contain("L:0C", "t[0] should be 12");
    }

    [Test]
    public void BoundTuple_LenIsElementCount()
    {
        // len(t) = 2 → M:02
        var uno = Boot();
        uno.RunUntilSerial(uno.Serial, s => s.Contains("M:02\n"), maxMs: 300);
        uno.Serial.Text.Should().Contain("M:02", "len(t) should be 2");
    }

    [Test]
    public void BoundTuple_IteratesElements()
    {
        // for x in t: 12 + 13 = 25 = 0x19 → N:19
        var uno = Boot();
        uno.RunUntilSerial(uno.Serial, s => s.Contains("N:19\n"), maxMs: 300);
        uno.Serial.Text.Should().Contain("N:19", "sum of t's elements should be 25");
    }
}
