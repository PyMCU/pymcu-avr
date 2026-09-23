# dht_simpletest.py shape from the Adafruit bundle, adapted two ways: the pin is
# board.D2 (the wire the testbench's DHT22 simulator sits on), and the printed
# value is narrowed before use -- `dhtDevice.temperature` is a
# Union[int, float, None], and PyMCU refuses an unnarrowed union read on purpose
# (RFC 0009): `isinstance(t, float)` is the tag compare that picks the float arm.
#
# Under the CPython oracle the same prints come out of plain reads; the oracle
# script prints the identical strings.
import time

import board

import adafruit_dht

dhtDevice = adafruit_dht.DHT22(board.D2)

while True:
    try:
        t = dhtDevice.temperature
        h = dhtDevice.humidity
        if isinstance(t, float) and isinstance(h, float):
            temperature_f = t * (9 / 5) + 32
            print("Temp:", temperature_f, "F /", t, "C    Humidity:", h, "%")
        elif t is None or h is None:
            print("no reading yet")
        else:
            print("unexpected member")
    except RuntimeError:
        print("read error")
    time.sleep(0.2)
