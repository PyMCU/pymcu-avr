using FluentAssertions;
using NUnit.Framework;
using Avr8Sharp.TestKit.Boards;
using Avr8Sharp.TestKit;

namespace PyMCU.IntegrationTests.Tests.AVR;

/// <summary>
/// Integration tests for fixtures/buffer-param (PyMCU#150).
///
/// `buf[i]` on a parameter with no type annotation used to be read as a REGISTER BIT index
/// rather than an element index. A run-time index failed to build, with a message naming
/// neither the buffer nor the parameter and describing an operation the program does not
/// contain. A CONSTANT index was worse: it compiled, silently, into a bit test of the
/// buffer's ADDRESS, so the program ran and answered 0 or 1 where a byte was expected.
///
/// These checkpoints therefore measure VALUES, not just that it builds: a wrong answer on a
/// clean build is the shape this bug had.
///
/// Data-space addresses used: GPIOR0 = 0x3E, GPIOR1 = 0x4A, GPIOR2 = 0x4B
/// </summary>
[TestFixture]
public class BufferParamTests
{
    private SimSession _session = null!;

    private const int GPIOR0_ADDR = 0x3E;
    private const int GPIOR1_ADDR = 0x4A;
    private const int GPIOR2_ADDR = 0x4B;

    [OneTimeSetUp]
    public void BuildFirmware() => _session = new SimSession(PymcuCompiler.BuildFixture("buffer-param"));

    private static void SkipBreaks(ArduinoUnoSimulation uno, int count)
    {
        for (var i = 0; i < count; i++)
        {
            uno.RunToBreak();
            uno.RunInstructions(1); // step over the BREAK opcode
        }
    }

    private ArduinoUnoSimulation AtCheckpoint(int n)
    {
        var uno = _session.Reset();
        SkipBreaks(uno, n - 1);
        uno.RunToBreak();
        return uno;
    }

    [Test]
    public void ModuleLevelBuffer_ReachesTheCalleeAsBytes()
    {
        var uno = AtCheckpoint(1);

        uno.Data[GPIOR0_ADDR].Should().Be(18,
            "5 + 6 + 7 -- a run-time index over a module-level buffer used not to build at all");
        uno.Data[GPIOR1_ADDR].Should().Be(5,
            "buf[0] is the first BYTE; it used to compile into bit 0 of the buffer's address, " +
            "which answers 0 or 1 and never 5");
    }

    [Test]
    public void LocalBuffer_AnswersTheSame()
    {
        var uno = AtCheckpoint(2);

        uno.Data[GPIOR0_ADDR].Should().Be(6, "1 + 2 + 3");
        uno.Data[GPIOR1_ADDR].Should().Be(3, "buf[2] of a local buffer is its third byte");
    }

    [Test]
    public void TheCalleeCanWriteThroughThePointerItWasHanded()
    {
        var uno = AtCheckpoint(3);

        uno.Data[GPIOR0_ADDR].Should().Be(10, "fill() wrote i + 10 into each element");
        uno.Data[GPIOR1_ADDR].Should().Be(12);
        uno.Data[GPIOR2_ADDR].Should().Be(33,
            "10 + 11 + 12 -- the caller sees the bytes the callee stored, so the pointer is " +
            "the buffer's and not a copy");
    }

    [Test]
    public void AnInlineCallee_AgreesWithTheOutlinedOne()
    {
        // The annotated and unannotated spellings, and the inline and outlined callees, all
        // have to reach the same bytes; the divergence between them is what #150 reported.
        var uno = AtCheckpoint(4);

        uno.Data[GPIOR0_ADDR].Should().Be(33, "the module-level buffer, after fill()");
        uno.Data[GPIOR1_ADDR].Should().Be(6, "the local buffer");
    }
}
