using FluentAssertions;
using NUnit.Framework;
using Avr8Sharp.TestKit.Boards;
using Avr8Sharp.TestKit;

namespace PyMCU.IntegrationTests.Tests.AVR;

/// <summary>
/// machine.ADC(0), the channel number MicroPython documents, was refused inside the layer's
/// own _adc_channel_port: its match had no `case _:` arm, so the compiler saw a path that
/// reaches the end of a function declared to return str, and the channel was a compile-time
/// constant that selects an arm (PyMCU/pymcu#469).
///
/// Pinned against the spelling that already worked: channel 0 is A0 = PC0 on the Uno, so
/// ADC(0) must be the same image as ADC("PC0"). An equality is what catches a shape that
/// compiles and reads the wrong channel.
/// </summary>
[TestFixture]
public class CompatMpAdcChannelTests
{
    [Test]
    public void AdcFromAChannelNumber_IsTheSameImageAsFromItsPortName()
    {
        var byChannel = PymcuCompiler.BuildFixture("compat-mp-adc-channel");
        var byName    = PymcuCompiler.BuildFixture("compat-mp-adc-port-name");
        byChannel.Should().Be(byName,
            "channel 0 is A0 = PC0 on the Uno, so ADC(0) and ADC(\"PC0\") are the same program");
    }

    [Test]
    public void AdcFromAChannelNumber_IsTheSameImageAsFromItsPortName_PyParser()
    {
        var byChannel = PymcuCompiler.BuildFixturePyParser("compat-mp-adc-channel");
        var byName    = PymcuCompiler.BuildFixturePyParser("compat-mp-adc-port-name");
        byChannel.Should().Be(byName,
            "the Python front end must map the channel to the same port");
    }
}
