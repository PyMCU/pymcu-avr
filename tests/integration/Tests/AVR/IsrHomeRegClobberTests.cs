using System;
using System.Linq;
using Avr8Sharp.TestKit;
using FluentAssertions;
using NUnit.Framework;

namespace PyMCU.IntegrationTests.Tests.AVR;

/// <summary>
/// fixtures/isr-home-reg-clobber (pymcu-avr#22): an interrupt handler must not be able to
/// change a value mainline code is holding in a register.
///
/// The ISR prologue pushed a FIXED set of caller-saved registers, chosen before register
/// allocation ran. The allocator is free to home a handler's locals in R2-R15, which is the
/// callee-saved pool mainline code also uses, and it hands out one home per NAME -- which is
/// not one home per function: an @inline expanded in both the handler and main is the same
/// name in both, so both expansions get the same register. Nothing saved it, so the first
/// interrupt destroyed whatever main was keeping there, silently.
///
/// Found on a real Arduino Uno with pulseio: the capture handler's `now: uint16 = TCNT1.value`
/// sat in R6:R7 while main held a live value in R7. The emulator never saw it because the pin
/// produced no edge; here Timer0 overflows on its own, so the interrupt fires either way.
///
/// The fixture's main runs a countdown inside the shared expansion, long enough for several
/// Timer0 overflows, and prints the two totals. They are arithmetic, not timing: 28879 + 20000
/// and 4560 + 100. Before the fix the first one came back as whatever the handler had left in
/// the register.
/// </summary>
[TestFixture]
public class IsrHomeRegClobberTests
{
    private static SimSession _session = null!;
    private static SimSession _pySession = null!;

    [OneTimeSetUp]
    public void BuildFirmware()
    {
        _session   = new SimSession(PymcuCompiler.BuildFixture("isr-home-reg-clobber"));
        _pySession = new SimSession(PymcuCompiler.BuildFixturePyParser("isr-home-reg-clobber"));
    }

    private static string Output(SimSession s)
    {
        var uno = s.Reset();
        uno.RunMilliseconds(400);
        return uno.Serial.Text;
    }

    private static readonly string[] Expected = { "48879", "4660" };

    [Test]
    public void TheTotalsAreTheOnesTheArithmeticGives()
        => Lines(_session).Should().Equal(Expected,
            "the handler writes R2-R15 homes that main is also using; saving them is what "
            + "keeps main's running total its own");

    [Test]
    public void TheSameThroughThePythonFrontEnd()
        => Lines(_pySession).Should().Equal(Expected);

    private static string[] Lines(SimSession s)
        => Output(s).Split('\n', StringSplitOptions.RemoveEmptyEntries)
                    .Select(l => l.Trim()).ToArray();
}
