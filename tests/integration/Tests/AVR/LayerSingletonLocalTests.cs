using System;
using System.Linq;
using Avr8Sharp.TestKit;
using FluentAssertions;
using NUnit.Framework;

namespace PyMCU.IntegrationTests.Tests.AVR;

/// <summary>
/// fixtures/layer-singleton-local (PyMCU#259): a compat-layer namespace bound to a local keeps
/// being that namespace.
///
/// `alarm.pin`, `alarm.time`, `microcontroller.cpu` and `microcontroller.watchdog` are
/// module-level singletons; that is how the layers give CircuitPython its dotted spelling, and
/// binding one to a name is what upstream's own documentation shows. It was filed as a
/// VALUE-TRACKING alias, which is cleared at every label because a copied scalar may not have
/// run on both paths of a join. A loop emits a label, so by the first use inside the loop the
/// name had stopped being an object and the call was refused as a method on an integer -- while
/// the same two lines with no loop between them compiled.
/// </summary>
[TestFixture]
public class LayerSingletonLocalTests
{
    private static SimSession _session = null!;
    private static SimSession _pySession = null!;

    [OneTimeSetUp]
    public void BuildFirmware()
    {
        _session   = new SimSession(PymcuCompiler.BuildFixture("layer-singleton-local"));
        _pySession = new SimSession(PymcuCompiler.BuildFixturePyParser("layer-singleton-local"));
    }

    private static string[] Lines(SimSession s)
    {
        var uno = s.Reset();
        uno.RunMilliseconds(400);
        return uno.Serial.Text.Split('\n', StringSplitOptions.RemoveEmptyEntries)
                              .Select(l => l.Trim()).ToArray();
    }

    private static readonly string[] Cpython = { "16000000", "16000000", "done" };

    [Test]
    public void TheNamespaceSurvivesTheLoop()
        => Lines(_session).Should().Equal(Cpython,
            "which object a name stands for does not depend on which path ran, so the binding "
            + "has to survive a label");

    [Test]
    public void TheSameThroughThePythonFrontEnd()
        => Lines(_pySession).Should().Equal(Cpython);
}
