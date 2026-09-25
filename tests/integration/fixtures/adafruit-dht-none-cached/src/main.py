# The verbatim simpletest's None path -- the one wire stimulus that reaches it.
#
# adafruit_dht.measure() stamps _last_called BEFORE the wire read and only fills
# _temperature/_humidity on success, so a failed read leaves the fields None
# under a nonzero _last_called. A second property read inside the driver's 2 s
# cache window then returns early WITHOUT touching the wire and yields
# _temperature -- still None. In the verbatim loop the retry always lands past
# the window (the failed read's own 0.25 s capture plus the handler's
# time.sleep(2.0) put it ~2.27 s out), which is why the verbatim program can
# only ever print the RuntimeError text on a dead sensor -- this program is the
# same arithmetic and handler chain with the second read inside the window.
import time

import board

import adafruit_dht

dhtDevice = adafruit_dht.DHT22(board.D2)

# measure() on the very first call can run before the first Timer0 tick, which
# would stamp _last_called = 0.0 and keep the `== 0` fast path open; the sleep
# makes the stamp nonzero so the second read evaluates the cache window.
time.sleep(0.05)

try:
    temperature_c = dhtDevice.temperature
except RuntimeError:
    pass  # dead sensor: _last_called stamped, _temperature stays None

try:
    # verbatim simpletest lines: the property returns _temperature -- None --
    # and the arithmetic faults exactly as CPython's does on a None read.
    temperature_c = dhtDevice.temperature
    temperature_f = temperature_c * (9 / 5) + 32
    humidity = dhtDevice.humidity
    print(f"Temp: {temperature_f:.1f} F / {temperature_c:.1f} C    Humidity: {humidity}% ")
except RuntimeError as error:
    print(error.args[0])
except Exception as error:
    dhtDevice.exit()
    raise error
