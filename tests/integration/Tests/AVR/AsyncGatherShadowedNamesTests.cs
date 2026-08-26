// SPDX-License-Identifier: MIT
using FluentAssertions;
using NUnit.Framework;
using Avr8Sharp.TestKit.Boards;
using Avr8Sharp.TestKit;

namespace PyMCU.IntegrationTests.Tests.AVR;

/// <summary>
/// A coroutine may be called `a` or `b`, which are also the parameter names of
/// asyncio.gather. The async desugar names each coroutine's state-machine class after the
/// coroutine, so the parameter is bound to an instance of a class sharing its own name, and
/// `a.poll()` inside the expansion used to resolve to the class rather than to the instance.
/// The program did not build at all: `call to undefined function 'asyncio_a_poll'`.
/// </summary>
[TestFixture]
public class AsyncGatherShadowedNamesTests
{
    private SimSession _session = null!;

    [OneTimeSetUp]
    public void BuildFirmware() =>
        _session = new SimSession(PymcuCompiler.BuildFixture("async-gather-shadowed-names"));

    [Test]
    public void GatherCompletesWithCoroutinesNamedAAndB()
    {
        var uno = Sim();
        uno.RunUntilSerial(uno.Serial, "T:OK\n", maxMs: 500);
        uno.Serial.Should().ContainLine("T:OK");
    }

    [Test]
    public void TheTwoCoroutinesInterleaveByTheirOwnPeriods()
    {
        var uno = Sim();
        uno.RunUntilSerial(uno.Serial, "T:OK\n", maxMs: 500);
        // a() at 10/20/30/40/50/60 ms, b() at 25/50 ms. This is the assertion that
        // discriminates: a build that resolved the parameter to the wrong instance would
        // still print eight markers, in the wrong order or from one coroutine twice.
        uno.Serial.Text.Should().Contain("GS\nA\nA\nB\nA\nA\nB\nA\nA\n");
    }

    private ArduinoUnoSimulation Sim() => _session.Reset();
}
