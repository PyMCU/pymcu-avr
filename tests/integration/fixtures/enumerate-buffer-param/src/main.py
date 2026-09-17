# PyMCU -- enumerate-buffer-param: enumerate() over a buffer reached through
# inline parameter bindings, in the shapes the Adafruit drivers use.
#
# Shape 1 (adafruit_tcs34725): a class-level `_BUFFER = bytearray(8)` passed as
# `i2c.write(self._BUFFER)` through an inlined wrapper into an @inline callee that
# enumerates it. The alias resolves to the class-canonical name
# (`Sensor__BUFFER` / `mod_Sensor__BUFFER`) while the storage lives under the
# module init function (`main.Sensor__BUFFER`); without the normalization the
# loop rejected the argument as "not a fixed-size array". The same aliased name
# must also accept `buffer[i] = v` stores (readfrom_into's shape).
#
# Shape 2 (adafruit_bmp280): `i2c.write(bytes([register & 0xFF]))` -- a bytes()
# literal whose element is decided at RUN time. It binds no constant sequence;
# the argument materializes a hidden buffer the enumerate() then iterates. The
# buffer's elements must carry the evaluated value, not a zero.
#
# Expected UART output:
#   42
#   11
#   0
#   7
#   1      (fill() overwrote _BUFFER[0] with 0; write_reg sends 0 + 1)
#   done
from pymcu.hal.console import print
from pymcu.hal.uart import UART


class Bus:
    @inline
    def writeto(self, buffer, end: int = 65535):
        for i, b in enumerate(buffer):
            if i < end:
                print(b)

    @inline
    def readinto(self, buffer, n: int):
        for i, _ in enumerate(buffer):
            if i < n:
                buffer[i] = i * 7


class Device:
    def __init__(self, bus):
        self._bus = bus

    def __enter__(self):
        return self

    def __exit__(self):
        pass

    def write(self, buf, end: int = 0):
        self._bus.writeto(buf, end)

    def read(self, buf, n: int):
        self._bus.readinto(buf, n)


class Sensor:
    _BUFFER = bytearray(8)

    def __init__(self, bus):
        self._dev = Device(bus)

    def poke(self, val: int):
        with self._dev as bus:
            self._BUFFER[0] = val & 0xFF
            self._BUFFER[1] = 11
            bus.write(self._BUFFER, end=2)

    def fill(self, n: int):
        with self._dev as bus:
            bus.read(self._BUFFER, n)
            print(self._BUFFER[0])
            print(self._BUFFER[1])

    def write_reg(self):
        # bytes([expr]) with a run-time element: self._BUFFER[0] was stored by
        # poke() and folds to nothing, so the argument materializes a buffer.
        with self._dev as bus:
            bus.write(bytes([self._BUFFER[0] + 1]), 1)


uart = UART(9600)

s = Sensor(Bus())
s.poke(42)
s.fill(3)
s.write_reg()

print("done")

while True:
    pass
