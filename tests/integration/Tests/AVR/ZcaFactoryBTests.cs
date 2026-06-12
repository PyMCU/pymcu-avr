using Avr8Sharp.TestKit.Boards;
using FluentAssertions;
using NUnit.Framework;

namespace PyMCU.IntegrationTests.Tests.AVR;

/// <summary>
/// Integration tests for fixtures/zca-factory-b -- RFC 0001 Model B (register-packed handle).
/// A single-field ZCA has no runtime struct, so a non-@inline factory returns the instance's
/// packed field as a scalar handle in the return register; the use site tracks the result as
/// a handle instance whose @outline method receives that scalar as its self field. This fixes
/// the old `def make() -> Sensor: return Sensor(..)` link error WITHOUT forcing @inline.
///   make_sensor(20) -> handle 21; s.read() = 21*2 = 42 = '*'
///   make_sensor(40) -> handle 41; t.read() = 41*2 = 82 = 'R'
/// Reaching "*R" proves the handle crosses the call boundary at runtime and the shared body
/// computes each instance's result from its own handle.
/// </summary>
[TestFixture]
public class ZcaFactoryBTests
{
    private static SimSession _session = null!;

    [OneTimeSetUp]
    public void BuildFirmware() => _session = new SimSession(PymcuCompiler.BuildFixture("zca-factory-b"));

    [Test]
    public void FactoryHandle_CrossesBoundary_SharedMethodComputesPerInstance()
    {
        var uno = _session.Reset();
        uno.RunUntilSerial(uno.Serial, s => s.Contains("*R"), maxMs: 400);
        uno.Serial.Text.Should().Contain("FB", "boot banner is emitted");
        uno.Serial.Text.Should().Contain("*R",
            "factory handles 21 and 41 cross the call boundary; shared Sensor_read doubles " +
            "each -> 42 ('*'), 82 ('R')");
    }
}
