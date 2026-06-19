using FluentAssertions;
using NUnit.Framework;
using Avr8Sharp.TestKit.Boards;

namespace PyMCU.IntegrationTests.Tests.AVR;

/// <summary>
/// Integration tests for fixtures/global-init — a module-level mutable global with
/// a non-zero initializer (`_seed: uint16 = 3007`). It previously landed in BSS with
/// the initializer dropped and read 0; the fix injects the init into main(). The
/// fixture prints _seed (expects 3007) then increments it at runtime and prints
/// again (3008), proving it is a real RAM cell seeded to the literal value.
/// </summary>
[TestFixture]
public class GlobalInitTests
{
    private static SimSession _session = null!;

    [OneTimeSetUp]
    public void BuildFirmware() => _session = new SimSession(PymcuCompiler.BuildFixture("global-init"));

    [Test]
    public void NonZeroInitializer_IsWrittenAtStartup()
    {
        var uno = _session.Reset();
        uno.RunUntilSerial(uno.Serial, s => s.Contains("3007\n"), maxMs: 400);
        uno.Serial.Text.Should().Contain("3007", "the global is seeded to its literal, not 0");
    }

    [Test]
    public void Global_IsAMutableRamCell()
    {
        var uno = _session.Reset();
        uno.RunUntilSerial(uno.Serial, s => s.Contains("3007\n3008\n"), maxMs: 400);
        uno.Serial.Text.Should().Contain("3007\n3008", "the runtime increment proves it is RAM, not a constant");
    }
}
