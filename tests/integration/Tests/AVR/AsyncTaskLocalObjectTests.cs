// SPDX-License-Identifier: MIT
using FluentAssertions;
using NUnit.Framework;
using Avr8Sharp.TestKit.Boards;
using Avr8Sharp.TestKit;

namespace PyMCU.IntegrationTests.Tests.AVR;

/// <summary>
/// PyMCU/PyMCU#116. A name a coroutine uses only as a method receiver was not counted as used,
/// so it was never lifted into the coroutine's state and stayed a static local of poll(). Two
/// instances of the same coroutine then shared one object.
/// </summary>
[TestFixture]
public class AsyncTaskLocalObjectTests
{
    /// <summary>
    /// The discriminating values are 11 and 21: one per instance, each counting from its own
    /// seed. Sharing reported 21 and 22, which is also two numbers, so asserting "two lines
    /// arrive" would pass either way.
    /// </summary>
    [Test]
    public void Issue116_EachInstanceOfACoroutineKeepsItsOwnObject()
    {
        var uno = new SimSession(
            PymcuCompiler.BuildFixture("async-task-local-object")).Reset();
        uno.RunUntilSerial(uno.Serial, "END\n", maxMs: 400);

        uno.Serial.Text.Should().Contain("TLO\n11\n21\nEND\n");
    }
}
