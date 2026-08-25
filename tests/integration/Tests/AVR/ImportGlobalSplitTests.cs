using FluentAssertions;
using NUnit.Framework;

namespace PyMCU.IntegrationTests.Tests.AVR;

/// <summary>
/// `from mod import name` on a module global. The declared value must be what the program
/// reads before anything writes it, and a write from the module's own function must be what
/// it reads afterwards -- the same two answers the `import mod` spelling gives.
/// </summary>
[TestFixture]
public class ImportGlobalSplitTests
{
    private static SimSession _session = null!;

    [OneTimeSetUp]
    public void BuildFirmware() => _session = new SimSession(PymcuCompiler.BuildFixture("import-global-split"));

    private static string RunWithSeed(byte seed)
    {
        var uno = _session.Reset();
        uno.Data[0x3E] = seed;              // GPIOR0
        uno.RunMilliseconds(200);
        return uno.Serial.Text;
    }

    [Test]
    public void NothingWroteIt_ReadsTheDeclaredValue() => RunWithSeed(0).Should().Contain("IG\n7\n");

    [Test]
    public void TheModuleFunctionWroteIt_ReadsWhatItWrote() => RunWithSeed(20).Should().Contain("IG\n42\n");
}
