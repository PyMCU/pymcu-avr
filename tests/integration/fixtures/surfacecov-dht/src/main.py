# Whole-surface probe for adafruit_dht: every public member exercised.
# Under PyMCU this fixture is build-refused -- importing the module lowers the
# class eagerly and `Union[int, float, None]` on the temperature/humidity
# properties is a documented CompileError. The CPython oracle still runs the
# full program so the report records what each member should do.

import board
from adafruit_dht import DHT11, DHT22, DHT21

# Happy path: a scripted 81-pulse frame (45% / 25.0 C) answers the first read.
d11 = DHT11(board.D5)
print(d11.temperature)       # measure() + decode + property
print(d11.humidity)
print(d11.temperature)       # cached: < 2 s since last measure, no new frame
d11.exit()

# DHT22 two-byte fields, measure() called directly.
d22 = DHT22(board.D6)
d22.measure()
print(d22.temperature)       # 25.3
print(d22.humidity)          # 45.5
d22.exit()

# Checksum mismatch -> RuntimeError.
d11bad = DHT11(board.D7)
try:
    d11bad.measure()
    print("no error")
except RuntimeError as e:
    print("R:", e)
d11bad.exit()

# Exhausted script -> no pulses -> RuntimeError("DHT sensor not found").
d11gone = DHT11(board.D9)
try:
    print(d11gone.temperature)
    print("no error")
except RuntimeError as e:
    print("R:", e)
d11gone.exit()

# use_pulseio=False: bitbang exists only on Blinka/Linux; under CircuitPython
# (and under the oracle's non-Linux uname) the constructor refuses.
try:
    d11bb = DHT11(board.D10, use_pulseio=False)
    print("bb made")
except ValueError as e:
    print("V:", e)

# DHT21/AM2301: same decode as DHT22 with a wider capture buffer.
d21 = DHT21(board.D8)
print(d21.temperature)       # 22.1
print(d21.humidity)          # 60.2
d21.exit()

print("=DONE=")
