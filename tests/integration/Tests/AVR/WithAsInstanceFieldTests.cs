using Avr8Sharp.TestKit.Boards;
using FluentAssertions;
using NUnit.Framework;

namespace PyMCU.IntegrationTests.Tests.AVR;

/// <summary>
/// Integration tests for fixtures/with-as-instance-field.
///
/// PyMCU#454. `with mgr as v:` where __enter__ returns an instance of ANOTHER
/// class -- `return self.inner`, the bus_device.SPIDevice shape that hands back
/// its `self.spi` field -- left the bound name without a class, so a method call
/// on it resolved to a free function nothing emitted. The bound name must take
/// the returned instance's storage and its class.
///
/// Every expectation is CPython's answer for the same program.
/// </summary>
[TestFixture]
public class WithAsInstanceFieldTests
{
    private static SimSession _session = null!;

    [OneTimeSetUp]
    public void BuildFirmware() =>
        _session = new SimSession(PymcuCompiler.BuildFixture("with-as-instance-field"));

    private ArduinoUnoSimulation Boot()
    {
        var uno = _session.Reset();
        uno.RunUntilSerial(uno.Serial, "done\n", maxMs: 1000);
        return uno;
    }

    [Test]
    public void ModuleLevel_WithAs_CallsTheReturnedInstancesMethod()
    {
        Boot().Serial.Text.Should().Contain("7\n",
            "with m as i: i.read() must reach Inner.read on m.inner, not refuse");
    }

    [Test]
    public void InsideAMethod_TheBoundName_KeepsTheReturnedClass()
    {
        Boot().Serial.Text.Should().Contain("41\n",
            "with self._mgr as bus: bus.bump(2) must reach Inner.bump (39 + 2)");
    }
}

/// <summary>
/// The same fixture under the Python-frontend parser.
/// </summary>
[TestFixture]
public class WithAsInstanceFieldPyParserTests
{
    private static SimSession _session = null!;

    [OneTimeSetUp]
    public void BuildFirmware() =>
        _session = new SimSession(PymcuCompiler.BuildFixturePyParser("with-as-instance-field"));

    [Test]
    public void BothShapes_KeepTheReturnedClass()
    {
        var uno = _session.Reset();
        uno.RunUntilSerial(uno.Serial, "done\n", maxMs: 1000);
        uno.Serial.Text.Should().Contain("7\n41\n");
    }
}
