using Avr8Sharp.TestKit.Boards;
using FluentAssertions;
using NUnit.Framework;

namespace PyMCU.IntegrationTests.Tests.AVR;

/// <summary>
/// Integration tests for fixtures/zca-outline -- RFC 0001 Model A (@outline).
/// A ZCA method marked @outline is compiled ONCE as a shared subroutine that takes
/// the instance's runtime fields as leading parameters (self.base -> self_base param),
/// instead of being force-inlined per call site. Two Counter instances (base 65 and 97)
/// drive the SAME Counter_stepped body with different runtime args:
///   a.stepped(1) = 65 + 1 = 66 = 'B'
///   b.stepped(2) = 97 + 2 = 99 = 'c'
/// Reaching "OLBc" over UART proves the outlined call passes each instance's field
/// value correctly and the shared body computes the right result per call.
/// </summary>
[TestFixture]
public class ZcaOutlineTests
{
    private static SimSession _session = null!;

    [OneTimeSetUp]
    public void BuildFirmware() => _session = new SimSession(PymcuCompiler.BuildFixture("zca-outline"));

    [Test]
    public void Outline_TwoInstances_ShareBodyWithRuntimeFields()
    {
        var uno = _session.Reset();
        uno.RunUntilSerial(uno.Serial, s => s.Contains("Bc"), maxMs: 400);
        uno.Serial.Text.Should().Contain("OL", "boot banner is emitted");
        uno.Serial.Text.Should().Contain("Bc",
            "outlined Counter_stepped receives each instance's runtime base (65, 97) " +
            "and adds k (1, 2) -> 'B','c'");
    }
}
