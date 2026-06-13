// SPDX-License-Identifier: MIT
using FluentAssertions;
using NUnit.Framework;
using Avr8Sharp.TestKit.Boards;
using Avr8Sharp.TestKit;

namespace PyMCU.IntegrationTests.Tests.AVR;

/// <summary>
/// Integration test for runtime-offset pointers: ptr(BASE + offset).value through
/// Store/Load indirect. Writes 40 to a free SRAM slot via the pointer, augments it
/// by 2 (read-modify-write), reads it back through a freshly computed pointer to the
/// same address, and emits the result over UART. A correct round-trip yields 42.
/// </summary>
[TestFixture]
public class PtrRuntimeTests
{
    private SimSession _session = null!;

    [OneTimeSetUp]
    public void BuildFirmware() => _session = new SimSession(PymcuCompiler.BuildFixture("ptr-runtime"));

    [Test]
    public void RuntimePointer_WriteAugAssignRead_RoundTrips()
    {
        var uno = _session.Reset();
        uno.RunUntilSerialBytes(uno.Serial, 4, maxMs: 500);  // "PR\n" (3) + result byte
        var bytes = uno.Serial.Bytes;
        bytes[^1].Should().Be(42, "40 written, += 2 via indirect RMW, read back through ptr(BASE+off)");
    }
}
