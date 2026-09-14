using Avr8Sharp.TestKit;
using Avr8Sharp.TestKit.Boards;
using FluentAssertions;
using NUnit.Framework;

namespace PyMCU.IntegrationTests.Tests.AVR;

/// <summary>
/// fixtures/compat-cp-digitalio-direction (PyMCU#309): `direction = INPUT` keeps the pull
/// the program set (None: floats), `deinit()` releases the pin with no pull. Measured on
/// an Arduino Uno before the fix: a pin driven high and switched to input stayed at 5 V.
/// </summary>
[TestFixture]
public class CompatCpDigitalioDirectionTests
{
    private const int Gpior1 = 0x4A, Gpior2 = 0x4B, Bit = 6;
    private static SimSession _session = null!;

    [OneTimeSetUp]
    public void BuildFirmware() => _session = new SimSession(PymcuCompiler.BuildFixture("compat-cp-digitalio-direction"));

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

    [Test] public void OutputTrueDrivesHigh() => Run()[0].Should().Be((1, 1));
    [Test] public void DirectionInputWithPullNoneFloats() => Run()[1].Should().Be((0, 0), "the output latch must not become a pull-up");
    [Test] public void DirectionInputKeepsAPullUpThatWasSet() => Run()[2].Should().Be((0, 1));
    [Test] public void DeinitReleasesWithoutPull() => Run()[3].Should().Be((0, 0));
}
