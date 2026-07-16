// SPDX-License-Identifier: MIT
using FluentAssertions;
using NUnit.Framework;
using Avr8Sharp.TestKit.Boards;
using Avr8Sharp.TestKit;

namespace PyMCU.IntegrationTests.Tests.AVR;

/// <summary>
/// Equal-length slice assignment: literal source, cross-array slice source,
/// SAME-array overlapping copy (snapshot semantics, Python-style) and a
/// whole-array source into a full slice.
/// </summary>
[TestFixture]
public class SliceAssignTests
{
    private SimSession _session = null!;

    [OneTimeSetUp]
    public void BuildFirmware() => _session = new SimSession(PymcuCompiler.BuildFixture("slice-assign"));

    [Test]
    public void LiteralCrossArrayOverlappingAndWhole()
    {
        var uno = _session.Reset();
        uno.RunUntilSerial(uno.Serial, "C:871\n", maxMs: 3000);
        var text = uno.Serial.Text;
        text.Should().Contain("A:19871");
        text.Should().Contain("B:871");
        text.Should().Contain("O:19198", "overlapping same-array copy must snapshot the source first");
    }
}
