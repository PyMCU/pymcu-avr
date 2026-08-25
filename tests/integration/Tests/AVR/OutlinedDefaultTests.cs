using FluentAssertions;
using NUnit.Framework;
using Avr8Sharp.TestKit.Boards;

namespace PyMCU.IntegrationTests.Tests.AVR;

/// <summary>
/// Integration tests for fixtures/outlined-default (PyMCU#96).
///
/// An outlined method is a real subroutine with a fixed parameter list. A call that omitted
/// an argument left that parameter unwritten, so the body read zero instead of the declared
/// default, and the program compiled clean.
///
/// Both calls are checked, because filling the default unconditionally would break the one
/// that passes a value.
/// </summary>
[TestFixture]
public class OutlinedDefaultTests
{
    private const int Gpior0Addr = 0x3E;
    private const int Gpior1Addr = 0x4A;
    private const int Gpior2Addr = 0x4B;

    private SimSession _session = null!;

    [OneTimeSetUp]
    public void BuildFirmware() => _session = new SimSession(PymcuCompiler.BuildFixture("outlined-default"));

    private ArduinoUnoSimulation Boot()
    {
        var uno = _session.Reset();
        uno.Data[Gpior0Addr] = 8;
        uno.RunToBreak();
        return uno;
    }

    [Test]
    public void OmittedArgument_UsesTheDeclaredDefault()
    {
        Boot().Data[Gpior1Addr].Should().Be(12, "8 + 4, not 8 + 0");
    }

    [Test]
    public void ExplicitArgument_StillWins()
    {
        Boot().Data[Gpior2Addr].Should().Be(38, "8 + 30");
    }
}
