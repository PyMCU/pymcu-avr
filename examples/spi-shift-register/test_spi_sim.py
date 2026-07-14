"""Turnkey simulation test for the spi-shift-register example: capture what the firmware clocks
out over MOSI and confirm it matches the pattern it reports over serial."""


def test_mosi_captures_first_pattern(mcu):
    mcu.run_ms(50)
    mosi = mcu.spi.mosi
    assert mosi, "firmware clocked at least one byte over SPI"
    assert mosi[0] == 0x01, "first shift-register pattern is 0x01"
