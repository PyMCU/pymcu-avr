"""Turnkey simulation test for the i2c-scanner example: place a slave on the bus and confirm the
scanner reports exactly that address."""


def test_scanner_finds_configured_slave(mcu):
    mcu.twi.set_slave(0x76, present=True)        # e.g. a BMP280
    mcu.run_until_serial(mcu.serial, "Done", max_ms=4000)

    text = mcu.serial.text
    assert "FOUND 0x76" in text
    assert "FOUND 0x77" not in text              # only the configured device answers


def test_empty_bus_finds_nothing(mcu):
    mcu.twi.set_slave(0x00, present=False)
    mcu.run_until_serial(mcu.serial, "Done", max_ms=4000)
    assert "FOUND" not in mcu.serial.text
