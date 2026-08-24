using FluentAssertions;
using NUnit.Framework;

namespace PyMCU.IntegrationTests.Tests.AVR;

/// <summary>
/// Importing a name a module does not export (PyMCU#54).
///
/// It used to surface at the CALL SITE as "call to undefined function
/// 'pymcu_hal_adc_ADC' (typo, or a missing import?)": a hint that sends the reader to check
/// an import that is right there, and a mangled symbol the user never wrote. The module is
/// known and so are its exports, so the message says which module, and either the near miss
/// or what it does export.
/// </summary>
[TestFixture]
public class ImportNotExportedTests
{
    [Test]
    public void UnexportedName_NamesTheModuleAndItsExports()
    {
        var ex = Assert.Throws<InvalidOperationException>(() => PymcuCompiler.BuildSource(
            "from pymcu.hal.adc import ADC\n\n\ndef main():\n    a = ADC(\"PC0\")\n"));

        ex!.Message.Should().Contain("'ADC' is not exported by pymcu.hal.adc");
        ex.Message.Should().Contain("AnalogPin", "the reader needs the name that does exist");
        ex.Message.Should().NotContain("pymcu_hal_adc_ADC", "the mangled symbol is internal");
    }

    [Test]
    public void NearMiss_IsSuggested()
    {
        var ex = Assert.Throws<InvalidOperationException>(() => PymcuCompiler.BuildSource(
            "from pymcu.hal.gpio import Pn\n\n\ndef main():\n    a = Pn(\"PB5\", 0)\n"));

        ex!.Message.Should().Contain("Did you mean 'Pin'?");
    }
}
