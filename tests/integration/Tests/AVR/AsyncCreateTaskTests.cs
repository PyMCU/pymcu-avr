// SPDX-License-Identifier: MIT
using FluentAssertions;
using NUnit.Framework;
using Avr8Sharp.TestKit.Boards;
using Avr8Sharp.TestKit;

namespace PyMCU.IntegrationTests.Tests.AVR;

/// <summary>
/// create_task() builds a compile-time task set: each call site gets its own global
/// coroutine instance and a running flag, and asyncio.run() becomes a round-robin over
/// exactly those plus the main coroutine. The two tasks here have different periods, so
/// the order of the markers is what proves each one keeps its own deadline and that a
/// task polled before it is due simply stays pending.
/// </summary>
[TestFixture]
public class AsyncCreateTaskTests
{
    private SimSession _session = null!;

    [OneTimeSetUp]
    public void BuildFirmware() => _session = new SimSession(PymcuCompiler.BuildFixture("async-create-task"));

    [Test]
    public void RunReturnsWhenTheMainCoroutineFinishes()
    {
        var uno = Sim();
        uno.RunUntilSerial(uno.Serial, "DONE\n", maxMs: 500);
        uno.Serial.Should().ContainLine("DONE");
    }

    [Test]
    public void TasksWithDifferentPeriodsInterleaveByDeadline()
    {
        var uno = Sim();
        uno.RunUntilSerial(uno.Serial, "DONE\n", maxMs: 500);
        // fast() fires at 10/20/30/40/50/60 ms, slow() at 25/50 ms, main ends at 80 ms.
        uno.Serial.Text.Should().Contain("CT\nF\nF\nS\nF\nF\nS\nF\nF\nDONE\n");
    }

    private ArduinoUnoSimulation Sim() => _session.Reset();
}
