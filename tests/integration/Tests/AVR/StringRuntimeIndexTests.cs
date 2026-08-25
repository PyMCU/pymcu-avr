using Avr8Sharp.TestKit.Boards;
using FluentAssertions;
using NUnit.Framework;

namespace PyMCU.IntegrationTests.Tests.AVR;

/// <summary>
/// Integration tests for fixtures/string-runtime-index (PyMCU#86).
///
/// Indexing a string with a run-time index was rejected with a message about bit indexing,
/// for a program containing no bit operation. It compiles now; this checks it also answers
/// with the character that is actually at that position.
/// </summary>
[TestFixture]
public class StringRuntimeIndexTests
{
    private static SimSession _session = null!;

    [OneTimeSetUp]
    public void BuildFirmware() => _session = new SimSession(PymcuCompiler.BuildFixture("string-runtime-index"));

    [Test]
    public void TheCharacterAtTheRunTimeIndexComesBack()
    {
        var uno = _session.Reset();
        uno.RunUntilSerial(uno.Serial, "done\n", maxMs: 800);
        uno.Serial.Text.Should().Contain("a\n", "GPIOR0 reads 0, so the index is 0 and \"abcd\"[0] is 'a'");
    }
}
