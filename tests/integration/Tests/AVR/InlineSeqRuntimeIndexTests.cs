using Avr8Sharp.TestKit.Boards;
using FluentAssertions;
using NUnit.Framework;

namespace PyMCU.IntegrationTests.Tests.AVR;

/// <summary>
/// Integration tests for fixtures/inline-seq-runtime-index (PyMCU#258).
///
/// A run-time subscript into a compile-time sequence reached through stacked
/// @inline parameters -- pulseio's `PulseOut.send(signal, n)` handing
/// `signal = [...]` down to `_PulseTrain.send(pulses, n)`, whose loop reads
/// `pulses[i]` -- fell through to the register-bit path and refused with
/// "Bit index must be constant". The read now goes to a materialised flash
/// table, gated on real stores into the sequence (handing the name to a call
/// is not one).
///
/// The fixture also covers `for b in buf` over a declared module array handed
/// to an @inline: the alias resolves function-qualified (`main.delays`) while
/// ScanGlobals registered the array bare (`delays`), and the for-in base
/// needed the same storage-key normalisation.
///
/// Every expectation is CPython's answer for the same program.
/// </summary>
[TestFixture]
public class InlineSeqRuntimeIndexTests
{
    private static SimSession _session = null!;

    [OneTimeSetUp]
    public void BuildFirmware() =>
        _session = new SimSession(PymcuCompiler.BuildFixture("inline-seq-runtime-index"));

    private ArduinoUnoSimulation Boot()
    {
        var uno = _session.Reset();
        uno.RunUntilSerial(uno.Serial, "done\n", maxMs: 1000);
        return uno;
    }

    [Test]
    public void RuntimeIndex_ThroughStackedInline_ReadsFlashTable()
    {
        Boot().Serial.Text.Should().Contain("560\n1690\n",
            "pulses[i] inside the innermost @inline must read signal's flash table");
    }

    [Test]
    public void ForIn_DeclaredModuleArray_ThroughInline()
    {
        Boot().Serial.Text.Should().Contain("100\n",
            "for b in delays must iterate the declared array, not a phantom slot");
    }

    [Test]
    public void RuntimeIndex_DeclaredModuleArray()
    {
        Boot().Serial.Text.Should().Contain("30\n",
            "delays[i + 1] with i = 1 must read element 2");
    }
}
