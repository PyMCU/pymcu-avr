using FluentAssertions;
using NUnit.Framework;
using Avr8Sharp.TestKit.Boards;
using Avr8Sharp.TestKit;

namespace PyMCU.IntegrationTests.Tests.AVR;

/// <summary>
/// Integration test for fixtures/millis-no-init.
///
/// The fixture never calls millis_init(): before the fix, millis() without an
/// explicit millis_init() read a Timer0 counter that nothing had armed, and
/// stayed frozen at 0 forever -- the exact silent failure millis()'s own
/// docstring raises to avoid everywhere else. The compiler now reports
/// [NEEDS_TIMEBASE] for a resolved millis() call too (previously only
/// micros()/ticks_ms()/monotonic() did), so the build driver stages the
/// millis_init() preamble on its own.
///
/// GPIOR0/GPIOR1 hold millis() (low/high byte) after a 20 ms delay_ms().
/// </summary>
[TestFixture]
public class MillisAutoArmTests
{
    private const int Gpior0Addr = 0x3E;
    private const int Gpior1Addr = 0x4A;

    private SimSession _session = null!;

    [OneTimeSetUp]
    public void BuildFirmware() => _session = new SimSession(PymcuCompiler.BuildFixture("millis-no-init"));

    [Test]
    public void MillisWithNoExplicitInit_IsNotFrozenAtZero()
    {
        var uno = _session.Reset();
        uno.RunToBreak(maxInstructions: 50_000_000);
        var lo = uno.Data[Gpior0Addr];
        var hi = uno.Data[Gpior1Addr];
        int count = lo | (hi << 8);
        // 20 ms of real delay_ms() against ~1.024 ms Timer0 overflows is at
        // least 15 ticks; a frozen counter reads exactly 0.
        count.Should().BeGreaterThanOrEqualTo(15,
            "the compiler must auto-arm Timer0 for a bare millis() call, " +
            "the same way it already does for micros()/monotonic()");
    }
}
