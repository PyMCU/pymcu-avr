using FluentAssertions;
using NUnit.Framework;
using Avr8Sharp.TestKit.Boards;

namespace PyMCU.IntegrationTests.Tests.AVR;

/// <summary>
/// Integration tests for fixtures/function-name-binding (PyMCU#69).
///
/// `f = a` binds the NAME at compile time, so the later call is direct and costs nothing.
/// Two names bound to two different functions are checked, because a single binding would
/// pass even if every name collapsed onto the last function seen.
///
/// The binding can only mean one function, so rebinding it -- or binding it inside a
/// run-time branch -- is refused rather than compiled into a program that ignores the
/// condition. That refusal is pinned here too.
/// </summary>
[TestFixture]
public class FunctionNameBindingTests
{
    private const int Gpior0Addr = 0x3E;
    private const int Gpior1Addr = 0x4A;

    private const string ConditionalRebind =
        "from pymcu.hal.gpio import Pin\n" +
        "from pymcu.types import uint8\n" +
        "\n" +
        "\n" +
        "def a() -> uint8:\n    return 1\n" +
        "\n" +
        "\n" +
        "def b() -> uint8:\n    return 2\n" +
        "\n" +
        "\n" +
        "def main():\n" +
        "    p = Pin(\"PB0\", Pin.IN)\n" +
        "    f = a\n" +
        "    if p.value():\n        f = b\n" +
        "    x: uint8 = f()\n";

    private SimSession _session = null!;

    [OneTimeSetUp]
    public void BuildFirmware() => _session = new SimSession(PymcuCompiler.BuildFixture("function-name-binding"));

    [Test]
    public void EachBoundName_CallsItsOwnFunction()
    {
        var uno = _session.Reset();
        uno.RunToBreak();

        uno.Data[Gpior0Addr].Should().Be(7, "f is bound to seven()");
        uno.Data[Gpior1Addr].Should().Be(9, "g is bound to nine(), not to the last function seen");
    }

    [Test]
    public void RebindingInsideARuntimeBranch_IsRefused()
    {
        var ex = Assert.Throws<InvalidOperationException>(() => PymcuCompiler.BuildSource(ConditionalRebind));

        ex!.Message.Should().Contain("bound to a function at compile time");
        ex.Message.Should().Contain("Callable", "the message must name the shape that does dispatch");
    }
}
