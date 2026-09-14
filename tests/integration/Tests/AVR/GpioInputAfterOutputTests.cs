using Avr8Sharp.TestKit;
using Avr8Sharp.TestKit.Boards;
using FluentAssertions;
using NUnit.Framework;

namespace PyMCU.IntegrationTests.Tests.AVR;

/// <summary>
/// fixtures/gpio-input-after-output (PyMCU#309): on the AVR the pull-up is the output
/// latch, so mode(IN) re-applies the pull the pin was asked for instead of leaving
/// whatever level was driven; mode(IN_PULLUP) sets it. See the fixture header.
/// </summary>
[TestFixture]
public class GpioInputAfterOutputTests
{
    private const int Gpior1 = 0x4A, Gpior2 = 0x4B, Bit = 6;
    private static SimSession _session = null!;

    [OneTimeSetUp]
    public void BuildFirmware() => _session = new SimSession(PymcuCompiler.BuildFixture("gpio-input-after-output"));

    private static (int Ddr, int Port)[] Run()
    {
        var uno = _session.Reset();
        var phases = new (int, int)[4];
        for (int i = 0; i < 4; i++)
        {
            if (i > 0) uno.RunInstructions(1);
            uno.RunToBreak();
            phases[i] = ((uno.Data[Gpior1] >> Bit) & 1, (uno.Data[Gpior2] >> Bit) & 1);
        }
        return phases;
    }

    [Test] public void AnInputAfterAHighOutputFloats() => Run()[0].Should().Be((0, 0), "no pull was asked for");
    [Test] public void AnInputKeepsThePullUpItAskedFor() => Run()[1].Should().Be((0, 1));
    [Test] public void PullZeroTakesItAway() => Run()[2].Should().Be((0, 0));
    [Test] public void InPullupSetsIt() => Run()[3].Should().Be((0, 1), "mode(IN_PULLUP) used to write 2 into the direction bit");
}
