// SPDX-License-Identifier: MIT
using FluentAssertions;
using NUnit.Framework;
using Avr8Sharp.TestKit.Boards;
using Avr8Sharp.TestKit;

namespace PyMCU.IntegrationTests.Tests.AVR;

/// <summary>
/// asyncio.gather(fast(), slow()) / asyncio.run(coro()) with the coroutine called
/// directly as the argument — the CPython spelling, which used to fail to compile
/// with "call to undefined function 'a_poll'" and needed the coroutine bound to a
/// name first. The state machine must reach the parameter with its class intact, so
/// poll() dispatches and the two coroutines interleave against the real time base.
/// </summary>
[TestFixture]
public class AsyncGatherDirectTests
{
    private SimSession _session = null!;

    [OneTimeSetUp]
    public void BuildFirmware() => _session = new SimSession(PymcuCompiler.BuildFixture("async-gather-direct"));

    [Test]
    public void GatherCompletes_AndAwaitsTakeRealTime()
    {
        var uno = Sim();
        uno.RunUntilSerial(uno.Serial, "T:OK\n", maxMs: 500);
        uno.Serial.Should().ContainLine("T:OK");
    }

    [Test]
    public void DirectlyCalledCoroutinesInterleave()
    {
        var uno = Sim();
        uno.RunUntilSerial(uno.Serial, "T:OK\n", maxMs: 500);
        // fast() fires at 10/20/30/40/50/60 ms, slow() at 35/70 ms.
        uno.Serial.Text.Should().Contain("ASY\nF\nF\nF\nS\nF\nF\nF\nS\n");
    }

    [Test]
    public void RunDrivesADirectlyCalledCoroutine()
    {
        var uno = Sim();
        uno.RunUntilSerial(uno.Serial, "T:OK\n", maxMs: 500);
        uno.Serial.Text.Should().Contain("S\nR\nR\n");
    }

    private ArduinoUnoSimulation Sim() => _session.Reset();
}
