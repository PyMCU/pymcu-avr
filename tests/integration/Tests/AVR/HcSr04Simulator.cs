using AVR8Sharp.Core.Peripherals;
using Avr8Sharp.TestKit.Boards;

namespace PyMCU.IntegrationTests.Tests.AVR;

/// <summary>
/// Models an HC-SR04 ultrasonic rangefinder: waits for the firmware's trigger pulse, then
/// after a fixed turnaround drives the echo pin high for a pulse width computed from a
/// requested distance, then low.
///
/// Built on the same primitives <see cref="Dht11Simulator"/> uses (<c>RunUntilMs</c> /
/// <c>SetPinValue</c> / <c>GetPinState</c>), no new TestKit surface. Mirrors avr8sharp's
/// Python <c>hc_sr04_echo</c> responder (bindings/python/src/avr8sharp/responder.py) so the
/// same acceptance numbers apply on either binding.
/// </summary>
internal sealed class HcSr04Simulator
{
    // Round-trip microseconds per centimetre the HC-SR04 datasheet assumes: distance_cm =
    // pulse_us * UsPerCm.
    private const double UsPerCm = 0.017;

    private readonly ArduinoUnoSimulation _sim;
    private readonly AvrIoPort _triggerPort;
    private readonly byte _triggerBit;
    private readonly AvrIoPort _echoPort;
    private readonly byte _echoBit;

    public HcSr04Simulator(
        ArduinoUnoSimulation sim,
        AvrIoPort triggerPort, byte triggerBit,
        AvrIoPort echoPort, byte echoBit)
    {
        _sim = sim;
        _triggerPort = triggerPort;
        _triggerBit = triggerBit;
        _echoPort = echoPort;
        _echoBit = echoBit;
    }

    /// <summary>
    /// Waits for the firmware to drive the trigger pin HIGH, then after
    /// <paramref name="echoDelayUs"/> (the sensor's internal turnaround before it starts
    /// chirping and listening) drives the echo pin high for the pulse width a real sensor
    /// would use for <paramref name="distanceCm"/>, then low.
    /// </summary>
    /// <returns>The echo pulse width actually driven, in microseconds, so a caller can
    /// compare it against what the firmware reports back.</returns>
    public double Echo(double distanceCm, double echoDelayUs = 450.0, double maxWaitMs = 500)
    {
        _sim.RunUntilMs(u => _triggerPort.GetPinState(_triggerBit) == PinState.High, maxWaitMs);
        _sim.RunMilliseconds(echoDelayUs / 1000.0);

        double pulseUs = distanceCm / UsPerCm;
        _echoPort.SetPinValue(_echoBit, true);
        _sim.RunMilliseconds(pulseUs / 1000.0);
        _echoPort.SetPinValue(_echoBit, false);
        return pulseUs;
    }
}
