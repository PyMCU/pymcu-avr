using Avr8Sharp.TestKit.Boards;
using FluentAssertions;
using NUnit.Framework;

namespace PyMCU.IntegrationTests.Tests.AVR;

/// <summary>
/// Integration tests for fixtures/inline-register-arg (PyMCU#144).
///
/// Passing REG.value to an @inline function bound that function's parameter to the register
/// through constantAddressVariables, and nothing cleared the binding before the next expansion
/// of the same @inline, so every later call re-read the register and ignored its own argument.
/// The other face of the same binding is a parameter declared uint8 that was aliased to the
/// address rather than copied, which made arithmetic on it a compile error naming a register
/// the program never wrote.
///
/// Against the unfixed compiler the fixture does not even build: it stops at
/// "'v' names a register, not its contents". With that half fixed but not the other, the build
/// succeeds and prints x=0 and j=1.
/// </summary>
[TestFixture]
public class InlineRegisterArgTests
{
    private static SimSession _session = null!;

    [OneTimeSetUp]
    public void BuildFirmware() => _session = new SimSession(PymcuCompiler.BuildFixture("inline-register-arg"));

    private string Boot()
    {
        var uno = _session.Reset();
        uno.RunUntilSerial(uno.Serial, "done\n", maxMs: 2000);
        return uno.Serial.Text;
    }

    [Test]
    public void ALocalPrintedAfterARegisterIsItsOwnValue()
    {
        Boot().Should().StartWith("r=0\nx=1\n", "the second call must not re-read GPIOR0");
    }

    [Test]
    public void AUint8ParameterReceivingARegisterReadCarriesTheContents()
    {
        Boot().Should().Contain("i=1\nj=8\ndone\n");
    }
}
