using FluentAssertions;
using NUnit.Framework;
using Avr8Sharp.TestKit.Boards;

namespace PyMCU.IntegrationTests.Tests.AVR;

/// <summary>
/// Integration tests for fixtures/yield-and-splat (PyMCU#67).
///
/// Two of the seven forms that used to be rejected with the parser's position rather than
/// the construct, and both were implementable rather than merely explainable: a bare `yield`
/// (the generator lowering already publishes 0 for a valueless suspension) and `f(*xs)` (the
/// elements can be spliced at compile time, since there is no run-time argument list).
///
/// Counted and summed rather than compiled: a generator that suspended the wrong number of
/// times, or a splice that dropped an element, would still build.
/// </summary>
[TestFixture]
public class YieldAndSplatTests
{
    private const int Gpior0Addr = 0x3E;
    private const int Gpior1Addr = 0x4A;
    private const int Gpior2Addr = 0x4B;

    private SimSession _session = null!;

    [OneTimeSetUp]
    public void BuildFirmware() => _session = new SimSession(PymcuCompiler.BuildFixture("yield-and-splat"));

    private ArduinoUnoSimulation Boot()
    {
        var uno = _session.Reset();
        uno.RunToBreak();
        return uno;
    }

    [Test]
    public void BareYield_SuspendsOncePerIteration()
    {
        Boot().Data[Gpior0Addr].Should().Be(3, "the generator yields three times with no value");
    }

    [Test]
    public void SplicedTuple_PassesEveryElement()
    {
        Boot().Data[Gpior1Addr].Should().Be(6, "suma(*(1, 2, 3))");
    }

    [Test]
    public void SplicedListLiteral_PassesEveryElement()
    {
        Boot().Data[Gpior2Addr].Should().Be(15, "suma(*[4, 5, 6])");
    }
}
