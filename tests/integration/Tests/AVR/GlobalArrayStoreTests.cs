using Avr8Sharp.TestKit.Boards;
using FluentAssertions;
using NUnit.Framework;

namespace PyMCU.IntegrationTests.Tests.AVR;

/// <summary>
/// Integration tests for fixtures/global-array-store (PyMCU#460).
///
/// `cfg = bytearray(4)` at module level registered two spellings: `main.cfg`,
/// which main's own init and reads emitted, and `cfg`, the scan's record. An
/// element store inside a function resolved to the bare `cfg` -- a second,
/// dead slot -- so `cfg[0] = 42` inside `def f(): global cfg` wrote nothing the
/// module-level read could see. The fix makes the module-scope init-function
/// spelling the one storage: the scan's bare name IS the storage for a
/// bytearray global, and a function that does not claim the name resolves to
/// it.
///
/// Both store spellings are covered (`global cfg` and the bare element store,
/// which Python needs no declaration for) plus `cfg[i] += v`.
/// </summary>
[TestFixture]
public class GlobalArrayStoreTests
{
    private static SimSession _session = null!;

    [OneTimeSetUp]
    public void BuildFirmware() =>
        _session = new SimSession(PymcuCompiler.BuildFixture("global-array-store"));

    private ArduinoUnoSimulation Boot()
    {
        var uno = _session.Reset();
        uno.RunUntilSerial(uno.Serial, "done\n", maxMs: 1000);
        return uno;
    }

    [Test]
    public void GlobalDeclared_IndexedStore_WritesModuleArray()
    {
        Boot().Serial.Text.Should().Contain("42\n",
            "cfg[0] = 42 under `global cfg` must reach the module array");
    }

    [Test]
    public void Undeclared_IndexedStore_WritesModuleArray()
    {
        Boot().Serial.Text.Should().Contain("7\n",
            "cfg[1] = 7 needs no `global` statement to mutate the module array");
    }

    [Test]
    public void AugmentedIndexedStore_WritesModuleArray()
    {
        Boot().Serial.Text.Should().Contain("49\n",
            "cfg[0] += 7 must read 42 back and store 49");
    }
}
