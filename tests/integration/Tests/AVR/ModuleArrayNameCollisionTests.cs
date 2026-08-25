using Avr8Sharp.TestKit.Boards;
using FluentAssertions;
using NUnit.Framework;

namespace PyMCU.IntegrationTests.Tests.AVR;

/// <summary>
/// Integration tests for fixtures/module-array-name-collision (PyMCU#167).
///
/// A function's own local array was registered under the BARE name and overwrote a module-level
/// array of the same name in the size registry. The AVR UART HAL declares `buf: uint8[32]`
/// inside uart_write_fmt; this fixture declares `buf: uint8[300]` at module level.
///
/// The store inside a function then carried count 32, took the narrow 8-bit index path, and
/// wrapped past index 255, while the read in main carried 300 and used the wide path. Two halves
/// of one array disagreeing about how wide the index is, on a clean build.
///
/// Against the unfixed compiler this prints 0 and 0.
/// </summary>
[TestFixture]
public class ModuleArrayNameCollisionTests
{
    private static SimSession _session = null!;

    [OneTimeSetUp]
    public void BuildFirmware() =>
        _session = new SimSession(PymcuCompiler.BuildFixture("module-array-name-collision"));

    private string Boot()
    {
        var uno = _session.Reset();
        uno.RunUntilSerial(uno.Serial, "done\n", maxMs: 3000);
        return uno.Serial.Text;
    }

    [Test]
    public void AWritePastByte255ReachesTheSlotItNamed()
    {
        Boot().Should().StartWith("99\n", "index 257 must not wrap into the low bytes");
    }

    [Test]
    public void TheLastElementOfAThreeHundredByteArrayIsItsOwn()
    {
        Boot().Should().Be("99\n77\ndone\n");
    }
}
