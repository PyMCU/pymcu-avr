using FluentAssertions;
using NUnit.Framework;
using Avr8Sharp.TestKit.Boards;

namespace PyMCU.IntegrationTests.Tests.AVR;

/// <summary>
/// Integration test for fixtures/name-main-guard.
///
/// `if __name__ == "__main__": main()` is the most universal idiom in Python, and it used
/// to be a compile error here: PyMCU calls the entry point itself, so the guard's body
/// added a second call and the cycle detector reported "main -> main" (PyMCU#65).
///
/// The idiom is accepted, and the counter is what proves the call was dropped rather than
/// duplicated: main increments GPIOR0 and breaks, so the register reads 1, not 2.
/// </summary>
[TestFixture]
public class NameMainGuardTests
{
    private const int Gpior0Addr = 0x3E;

    private SimSession _session = null!;

    [OneTimeSetUp]
    public void BuildFirmware() => _session = new SimSession(PymcuCompiler.BuildFixture("name-main-guard"));

    [Test]
    public void GuardedEntryPoint_RunsExactlyOnce()
    {
        var uno = _session.Reset();
        uno.RunToBreak();
        uno.Data[Gpior0Addr].Should().Be(1,
            "the guard's main() call is the one PyMCU already makes, not a second run");
    }
}
