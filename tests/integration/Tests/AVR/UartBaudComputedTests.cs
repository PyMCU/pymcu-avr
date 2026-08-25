using Avr8Sharp.TestKit.Boards;
using FluentAssertions;
using NUnit.Framework;

namespace PyMCU.IntegrationTests.Tests.AVR;

/// <summary>
/// Integration tests for fixtures/uart-baud-computed and fixtures/uart-baud-8mhz (PyMCU#136).
///
/// uart_init was an if/elif over five literal baud rates, with UBRR values hard-coded for
/// 16 MHz and no else. Any other rate left UBRR at 0, which is 1 Mbaud on a 16 MHz part, and
/// the `frequency` setting was ignored entirely, so 9600 on an 8 MHz part ran at 4808.
///
/// Each fixture copies UBRR0L into a GPIOR after init, so the divisor itself is what is
/// measured rather than whether characters happen to arrive: the emulator does not model a
/// baud mismatch, which is exactly why this went unnoticed.
/// </summary>
[TestFixture]
public class UartBaudComputedTests
{
    private const int Gpior0Addr = 0x3E;
    private const int Gpior1Addr = 0x4A;
    private const int Gpior2Addr = 0x4B;

    private static SimSession _session = null!;
    private static SimSession _session8 = null!;

    [OneTimeSetUp]
    public void BuildFirmware()
    {
        _session = new SimSession(PymcuCompiler.BuildFixture("uart-baud-computed"));
        _session8 = new SimSession(PymcuCompiler.BuildFixture("uart-baud-8mhz"));
    }

    [Test]
    public void ARateInTheOldTableIsUnchanged()
    {
        var uno = _session.Reset();
        uno.RunToBreak();
        uno.Data[Gpior0Addr].Should().Be(103, "9600 at 16 MHz has always been UBRR 103");
    }

    [Test]
    public void ARateTheTableDidNotHaveNoLongerLeavesTheDivisorAtZero()
    {
        var uno = _session.Reset();
        uno.RunToBreak();
        uno.Data[Gpior1Addr].Should().Be(207, "4800 at 16 MHz is UBRR 207, and 0 was 1 Mbaud");
    }

    [Test]
    public void TheExactDivisorRateAvrUsersPickWorks()
    {
        var uno = _session.Reset();
        uno.RunToBreak();
        uno.Data[Gpior2Addr].Should().Be(3, "250000 at 16 MHz divides exactly: UBRR 3");
    }

    [Test]
    public void TheConfiguredClockReachesTheDivisor()
    {
        var uno = _session8.Reset();
        uno.RunToBreak();
        uno.Data[Gpior0Addr].Should().Be(51, "9600 at 8 MHz is UBRR 51, not the 16 MHz 103");
    }
}
