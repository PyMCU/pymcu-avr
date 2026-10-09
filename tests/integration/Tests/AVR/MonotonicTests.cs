using FluentAssertions;
using NUnit.Framework;
using Avr8Sharp.TestKit.Boards;
using Avr8Sharp.TestKit;

namespace PyMCU.IntegrationTests.Tests.AVR;

/// <summary>
/// Integration test for fixtures/monotonic-basic.
///
/// `from time import monotonic` / `monotonic_ns` used to be an ImportError:
/// pymcu.time had neither, unlike CPython (both) or CircuitPython
/// (monotonic only). This fixture compiling at all is half the regression
/// test; the other half is that both strictly advance across a real delay
/// instead of reading a frozen or constant value -- and with no explicit
/// millis_init() anywhere in the source, confirming the auto-arm (both are
/// built on micros(), already a timebase reader) reaches them too.
///
/// GPIOR0 = 1 iff monotonic() strictly increased; GPIOR1 = 1 iff
/// monotonic_ns() strictly increased.
/// </summary>
[TestFixture]
public class MonotonicTests
{
    private const int Gpior0Addr = 0x3E;
    private const int Gpior1Addr = 0x4A;

    private SimSession _session = null!;

    [OneTimeSetUp]
    public void BuildFirmware() => _session = new SimSession(PymcuCompiler.BuildFixture("monotonic-basic"));

    [Test]
    public void MonotonicAndMonotonicNs_BothStrictlyAdvance()
    {
        var uno = _session.Reset();
        uno.RunToBreak(maxInstructions: 50_000_000);
        uno.Data[Gpior0Addr].Should().Be(1, "monotonic() must strictly increase across a real delay");
        uno.Data[Gpior1Addr].Should().Be(1, "monotonic_ns() must strictly increase across a real delay");
    }
}
