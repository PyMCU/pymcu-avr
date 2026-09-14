using FluentAssertions;
using NUnit.Framework;
using Avr8Sharp.TestKit.Boards;

namespace PyMCU.IntegrationTests.Tests.AVR;

/// <summary>
/// pwmio.PWMOut.frequency reprograms the timer (pymcu-circuitpython, PWMOut frequency setter).
///
/// Constructed with variable_frequency=True, the setter has to move TCCR0B to the nearest
/// prescaler for the new frequency, the way the constructor's frequency= does. It used to
/// store the value and leave the timer alone, so a program that swept the frequency heard
/// nothing change. A PWMOut constructed without variable_frequency refuses the setter at
/// compile time, which is CircuitPython's run-time error brought forward.
/// </summary>
[TestFixture]
public class CompatCpPwmioFrequencyTests
{
    private const int TCCR0B = 0x45;

    private SimSession _session = null!;

    [OneTimeSetUp]
    public void BuildFirmware() => _session = new SimSession(PymcuCompiler.BuildFixture("compat-cp-pwmio-frequency"));

    private byte Checkpoint(int n)
    {
        var uno = _session.Reset();
        for (int i = 0; i < n; i++)
        {
            if (i > 0) uno.RunInstructions(1);
            uno.RunToBreak();
        }
        return uno.Data[TCCR0B];
    }

    [TestCase(1, (byte)0x03, "frequency=1000 at construction is prescaler 64")]
    [TestCase(2, (byte)0x02, "pwm.frequency = 20000 moves to prescaler 8")]
    [TestCase(3, (byte)0x05, "pwm.frequency = 100 moves to prescaler 1024")]
    [TestCase(4, (byte)0x01, "pwm.frequency = 50000 moves to prescaler 1")]
    public void TheSetter_ReprogramsThePrescaler(int checkpoint, byte expected, string why)
        => Checkpoint(checkpoint).Should().Be(expected, why);

    [Test]
    public void WithoutVariableFrequency_TheSetterIsRefusedAtCompileTime()
    {
        var ex = Assert.Throws<InvalidOperationException>(() => PymcuCompiler.BuildSource(
            "import board\n" +
            "import pwmio\n" +
            "\n" +
            "def main():\n" +
            "    pwm = pwmio.PWMOut(board.D6, duty_cycle=32768, frequency=1000)\n" +
            "    pwm.frequency = 20000\n" +
            "\n" +
            "main()\n",
            File.ReadAllText(Path.Combine(PymcuCompiler.FixtureDir("compat-cp-pwmio-frequency"), "pyproject.toml"))));
        ex!.Message.Should().Contain("variable_frequency=True");
    }
}
