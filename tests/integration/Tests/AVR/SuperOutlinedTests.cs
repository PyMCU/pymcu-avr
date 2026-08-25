using FluentAssertions;
using NUnit.Framework;
using Avr8Sharp.TestKit.Boards;

namespace PyMCU.IntegrationTests.Tests.AVR;

/// <summary>
/// Integration tests for fixtures/super-outlined (PyMCU#97).
///
/// Two undecorated methods is the DEFAULT path (outlined by default), and there
/// `super().g()` read every inherited field as zero: the base method ran, it just never
/// received the instance state, so the override computed from 0 on a clean build.
///
/// The values are what prove it. A base that runs with zeroed fields still compiles, still
/// returns a number, and still looks like a working program.
/// </summary>
[TestFixture]
public class SuperOutlinedTests
{
    private const int Gpior0Addr = 0x3E;
    private const int Gpior1Addr = 0x4A;
    private const int Gpior2Addr = 0x4B;

    private SimSession _session = null!;

    [OneTimeSetUp]
    public void BuildFirmware() => _session = new SimSession(PymcuCompiler.BuildFixture("super-outlined"));

    private ArduinoUnoSimulation Boot()
    {
        var uno = _session.Reset();
        uno.Data[Gpior0Addr] = 5;
        uno.RunToBreak();
        return uno;
    }

    [Test]
    public void SuperCall_SeesTheInstanceField()
    {
        Boot().Data[Gpior1Addr].Should().Be(12, "(5 + 1) * 2, not (0 + 1) * 2");
    }

    [Test]
    public void SuperCall_SeesEveryInheritedField()
    {
        Boot().Data[Gpior2Addr].Should().Be(45, "5 + 40, with both fields arriving");
    }
}
