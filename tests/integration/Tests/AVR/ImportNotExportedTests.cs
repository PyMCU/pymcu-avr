using FluentAssertions;
using NUnit.Framework;

namespace PyMCU.IntegrationTests.Tests.AVR;

/// <summary>
/// Importing a name a module does not export (PyMCU#54, then PyMCU#158).
///
/// It used to surface at the CALL SITE as "call to undefined function
/// 'pymcu_hal_adc_ADC' (typo, or a missing import?)": a hint that sends the reader to check
/// an import that is right there, and a mangled symbol the user never wrote. #54 replaced that
/// with a message naming the module and either the near miss or what it does export.
///
/// #158 moved it EARLIER, to the import itself, because a name a module does not bind is an
/// ImportError and CPython raises one there. So these now assert against the import-site
/// message. The near miss survived the move on purpose: reporting at the import is only an
/// improvement if the reader who wrote `Pn` is still told `Pin` rather than handed a list.
/// </summary>
[TestFixture]
public class ImportNotExportedTests
{
    [Test]
    public void UnexportedName_NamesTheModuleAndItsExports()
    {
        var ex = Assert.Throws<InvalidOperationException>(() => PymcuCompiler.BuildSource(
            "from pymcu.hal.adc import ADC\n\n\ndef main():\n    a = ADC(\"PC0\")\n"));

        ex!.Message.Should().Contain("cannot import 'ADC' from 'pymcu.hal.adc'");
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
