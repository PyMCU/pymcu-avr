using Avr8Sharp.TestKit.Boards;
using PyMCU.TestKit;

namespace PyMCU.IntegrationTests;

/// <summary>
/// Manages a shared <see cref="ArduinoUnoSimulation"/> for a single test fixture.
///
/// The simulation is created once per fixture (in <c>[OneTimeSetUp]</c>) with the
/// firmware HEX already loaded into program flash.  Every call to <see cref="Reset"/>
/// restores the simulation to its exact power-on state so each test starts from a
/// clean slate without the overhead of allocating a new simulation object or
/// re-parsing the HEX file.
/// </summary>
/// <remarks>
/// <para><b>Reset procedure</b></para>
/// <list type="bullet">
///   <item>Restore the <c>Data</c> array (CPU registers, I/O registers, SRAM) from a
///         snapshot captured immediately after construction — before any firmware has
///         run — preserving the correct power-on values set by peripheral constructors
///         (e.g. <c>UCSRA.UDRE = 1</c> set by the <c>AvrUsart</c> constructor).</item>
///   <item>Call <c>Timer0/1/2.Reset()</c> to clear internal timer counters and
///         prescaler-divider state.</item>
///   <item>Call <c>Cpu.Reset()</c> to set PC=0, SP=top-of-SRAM, SREG=0, and clear
///         all pending interrupts and scheduled clock events (so stale timer/UART
///         callbacks from the previous test cannot fire into the next test).</item>
///   <item>Reset <c>Cpu.Cycles</c> to 0 so <c>RunMilliseconds</c> measures from the
///         start of the new test.</item>
///   <item>Clear the UART serial-probe receive buffer.</item>
/// </list>
/// <para>
/// <b>Note:</b> <c>SimSession</c> is not suitable for firmware that relies on EEPROM
/// peripheral state across tests.  <c>AvrEeprom</c> keeps internal write-timing counters
/// (<c>_writeCompleteCycles</c> / <c>_writeEnabledCycles</c>) that are not reset by
/// <c>Cpu.Reset()</c>.  Fixtures that exercise EEPROM should create a fresh
/// <see cref="ArduinoUnoSimulation"/> per test instead (see <c>EepromTests</c>).
/// </para>
/// </remarks>
public sealed class SimSession
{
    private readonly ArduinoUnoSimulation _sim;
    private readonly byte[] _dataSnapshot;

    /// <summary>
    /// Creates a session for the given firmware HEX content.
    /// </summary>
    /// <param name="hexContent">Intel HEX string returned by <see cref="PymcuCompiler"/>.</param>
    public SimSession(string hexContent)
    {
        _sim = new ArduinoUnoSimulation();

        // Snapshot taken BEFORE loading the HEX so it captures the peripheral
        // power-on defaults written by peripheral constructors (AvrUsart sets
        // UCSRA=32, UCSRC=6; Cpu.Reset sets SP=top, SREG=0).
        _dataSnapshot = (byte[])_sim.Data.Clone();

        _sim.WithHex(hexContent);
    }

    /// <summary>
    /// Resets the simulation to its power-on state and returns it, ready for a
    /// fresh test run.
    /// </summary>
    public ArduinoUnoSimulation Reset()
    {
        // 1. Restore all CPU registers, I/O registers, and SRAM to the initial state.
        Array.Copy(_dataSnapshot, _sim.Data, _dataSnapshot.Length);

        // 1b. RFC 0013 (docs/rfcs/0013-memory-model.md, PyMCU-rfc13), section
        // 7.2, ON BY DEFAULT since phase 0c ("static by exclusion", team-lead
        // directive 2026-09-30): fills R0-R31 (except R1) and SRAM
        // 0x100-0x8FF with a non-zero pattern (0xFF unless
        // PYMCU_FORCE_POISON_COLD_BOOT names a different byte), matching what
        // a real cold boot leaves undefined and avr8sharp's own zeroed state
        // does not. Measured before flipping this default: the full
        // integration suite run both poisoned and unpoisoned, 0 failures
        // either way (PyMCU-gemlife-lockinit and the compat-cp-life family's
        // own RecordWithStamps poison were the two fixtures that used to
        // fail poisoned; phase 0c's static-by-exclusion rule closes both).
        // SimSession is the entry point the MAJORITY of this suite's
        // fixtures use (UnoTwiTrace.Record and this file's own
        // poisonColdBoot-aware siblings are the exception, not the rule), so
        // this is what makes the whole suite demonstrate no firmware depends
        // on an assumption avr8sharp happens to make true for free, on every
        // ordinary run, not only when someone remembers to opt in.
        // ColdBootRealism.Poison only ever touches 0x00-0x1F and 0x100-0x8FF,
        // never the I/O register space (0x20-0xFF) the snapshot above just
        // restored a peripheral's real reset value into (e.g. AvrUsart's
        // UCSRA=32), so applying it here cannot undo that.
        var poisonEnv = Environment.GetEnvironmentVariable("PYMCU_FORCE_POISON_COLD_BOOT");
        byte poisonByte = poisonEnv != null && byte.TryParse(poisonEnv, out var forcedPoison)
            ? forcedPoison : (byte)0xFF;
        ColdBootRealism.Poison(_sim, poisonByte);

        // 2. Reset timer internal counters, dividers, and OCR shadow registers.
        _sim.Timer0.Reset();
        _sim.Timer1.Reset();
        _sim.Timer2.Reset();

        // 3. Reset CPU: PC=0, SP=top, SREG=0, clear pending interrupts and all
        //    scheduled clock events (prevents stale peripheral callbacks carrying over).
        _sim.Cpu.Reset();

        // 4. Reset the cycle counter so RunMilliseconds(ms) measures from 0.
        _sim.Cpu.Cycles = 0;

        // 5. Clear the UART receive buffer captured by the serial probe.
        _sim.Serial.Clear();

        // 6. Hold the TWI bus lines high, the state a wired-and-pulled-up bus
        //    sits in at idle. The GPIO model drives PIN only from what a test
        //    injects -- the internal pull-ups the firmware just enabled do not
        //    raise it -- and CircuitPython's busio.I2C reads the lines at
        //    construction and refuses a low one. A test that wants the bus
        //    held down sets the pin itself after Reset.
        _sim.PortC.SetPinValue(4, true);   // SDA
        _sim.PortC.SetPinValue(5, true);   // SCL

        return _sim;
    }
}
