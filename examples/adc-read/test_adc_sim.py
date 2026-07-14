"""Turnkey simulation test for the adc-read example: inject an analog voltage on A0 and check
the firmware's 8-bit UART output tracks it."""

import pytest


@pytest.mark.parametrize("volts,expected", [(0.0, 0), (2.5, 128), (5.0, 255)])
def test_adc_read_tracks_input_voltage(mcu, volts, expected):
    mcu.adc.set_channel(0, volts)        # A0 == ADC channel 0
    mcu.run_ms(150)
    payload = mcu.serial.bytes.split(b"\n", 1)[1]   # after the "ADC\n" banner
    assert payload, "firmware produced an ADC reading"
    assert abs(payload[0] - expected) <= 1
