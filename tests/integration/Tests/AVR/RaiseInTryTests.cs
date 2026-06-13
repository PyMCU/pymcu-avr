// SPDX-License-Identifier: MIT
using FluentAssertions;
using NUnit.Framework;
using Avr8Sharp.TestKit.Boards;
using Avr8Sharp.TestKit;

namespace PyMCU.IntegrationTests.Tests.AVR;

/// <summary>
/// Integration tests for the raise-in-try fixture.
///
/// A `raise` lexically inside a `try` body (NOT routed through a function call)
/// must be caught by the enclosing `except` in the SAME function.
///
/// Regression guard: such a raise used to lower to the cross-function error
/// epilogue (`LDI R22,code; SET; RET`), which returned from the function instead
/// of jumping to the local catch dispatcher — the `except` was skipped and `main`
/// executed a stray `RET` off an empty stack. It now lowers to
/// `LDI R22,code; JMP catch` (no SET, no RET): delivered straight to the local
/// dispatcher, T untouched.
/// </summary>
[TestFixture]
public class RaiseInTryTests
{
    private SimSession _session = null!;

    [OneTimeSetUp]
    public void BuildFirmware()
        => _session = new SimSession(PymcuCompiler.BuildFixture("raise-in-try"));

    private ArduinoUnoSimulation FullRun(int maxMs = 5000)
    {
        var uno = _session.Reset();
        uno.RunUntilSerial(uno.Serial, "DONE\n", maxMs: maxMs);
        return uno;
    }

    [Test]
    public void Boot_PrintsBanner()
    {
        var uno = _session.Reset();
        uno.RunUntilSerial(uno.Serial, "RT\n", maxMs: 500);
        uno.Serial.Should().ContainLine("RT");
    }

    [Test]
    public void A_DirectRaise_IsCaughtLocally()
    {
        var uno = FullRun();
        uno.Serial.Should().ContainLine("A:caught",
            "an unconditional raise in the try body must jump to the local catch dispatcher");
    }

    [Test]
    public void A_DirectRaise_DeadCodeAfterRaiseNotExecuted()
    {
        var uno = FullRun();
        uno.Serial.Text.Should().NotContain("A:miss",
            "the statement after an unconditional raise is unreachable");
    }

    [Test]
    public void B_NoRaise_HappyPathCompletes()
    {
        var uno = FullRun();
        uno.Serial.Should().ContainLine("B:ok");
        uno.Serial.Text.Should().NotContain("B:miss");
    }

    [Test]
    public void C_RaiseNestedInIf_IsCaughtLocally()
    {
        var uno = FullRun();
        uno.Serial.Should().ContainLine("C:caught",
            "a raise nested inside an if inside the try body must still be caught locally");
        uno.Serial.Text.Should().NotContain("C:miss");
    }

    [Test]
    public void Reaches_DONE_NoStrayReturn()
    {
        // If the local raise still emitted a stray RET, main would return off an
        // empty stack and never reach DONE.
        var uno = FullRun();
        uno.Serial.Should().ContainLine("DONE",
            "execution must continue past the try blocks (no stray RET from a local raise)");
    }
}
