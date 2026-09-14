using FluentAssertions;
using NUnit.Framework;
using Avr8Sharp.TestKit.Boards;

namespace PyMCU.IntegrationTests.Tests.AVR;

/// <summary>
/// fixtures/compat-cp-with-as-method (PyMCU#305): a method called on the name bound by
/// `with ... as` reaches the manager's class.
///
/// The name is a pure alias of the context manager, and only the field path followed that
/// alias. A method call looked the bare name up in the instance registry, found nothing, and
/// degraded into a free function: `pin.switch_to_output(True)` was refused as a call to an
/// undefined `pin_switch_to_output`.
/// </summary>
[TestFixture]
public class WithAsMethodCallTests
{
    private SimSession _session = null!;

    [OneTimeSetUp]
    public void BuildFirmware() =>
        _session = new SimSession(PymcuCompiler.BuildFixture("compat-cp-with-as-method"));

    [Test]
    public void TheMethodRunsOnTheManagerAndTheBlockStillReleasesThePin()
    {
        var uno = _session.Reset();
        uno.RunUntilSerial(uno.Serial, "END\n", maxMs: 600);
        uno.Serial.Text.Should().Contain("in 64 64", "D6 is an output driven high inside the block");
        uno.Serial.Text.Should().Contain("out 0", "__exit__ calls deinit(), which returns it to an input");
    }
}
