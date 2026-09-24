# dht_simpletest.py shape, minus the Fahrenheit arithmetic the compiler still
# refuses on an unnarrowed Optional: `print(dht.temperature)` itself is the RFC
# 0009 decision-7 read -- the tag picks the member's repr, None prints "None".
# The oracle prints the same two values under CPython float rules.
import time

import board

import adafruit_dht

dhtDevice = adafruit_dht.DHT22(board.D2)

while True:
    try:
        print(dhtDevice.temperature, dhtDevice.humidity)
        print(f"t={dhtDevice.temperature} h={dhtDevice.humidity}")
    except RuntimeError:
        print("read error")
    time.sleep(0.2)
