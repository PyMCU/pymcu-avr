// SPDX-License-Identifier: MIT
using FluentAssertions;
using NUnit.Framework;
using Avr8Sharp.TestKit.Boards;
using Avr8Sharp.TestKit;

namespace PyMCU.IntegrationTests.Tests.AVR;

/// <summary>
/// Type inference for unannotated params/returns of outlined functions: widths
/// join from call-site evidence (uint16, signed int16, uint32) instead of the
/// historical uint8 default that silently truncated (scale(300,2) printed 88).
/// </summary>
[TestFixture]
public class InferTypesTests
{
    private SimSession _session = null!;

    [OneTimeSetUp]
    public void BuildFirmware() => _session = new SimSession(PymcuCompiler.BuildFixture("infer-types"));

    [Test]
    public void Uint16Params_NoTruncation()
    {
        var uno = Sim();
        uno.RunUntilSerial(uno.Serial, "R:600\n", maxMs: 3000);
        uno.Serial.Should().ContainLine("R:600");
    }

    [Test]
    public void SignedAndWideInference()
    {
        var uno = Sim();
        uno.RunUntilSerial(uno.Serial, "Q:65540\n", maxMs: 3000);
        uno.Serial.Should().ContainLine("S:-15");
        uno.Serial.Should().ContainLine("Q:65540");
    }

    private ArduinoUnoSimulation Sim() => _session.Reset();
}
