# class-body-fit-grows-buffer: Adafruit adafruit_register._fit shape.
#
# _BUFFER = bytearray(1), then a class-body descriptor constructor calls
# _fit(2), which .extend()s. Class-body constructors run ahead of the
# module's own statements, and replaying bytearray(1) used to shrink the
# grown buffer back to 1 -- so _BUFFER[2] was IndexError size 1.
# That is what stopped adafruit_ina219 and adafruit_veml7700 after
# value <<= kept the setter parameter.
#
# WHAT DISCRIMINATES: 7 written at _BUFFER[2]. A compile that still
# treated the buffer as one byte would not build.
from pymcu.types import uint8
from pymcu.time import delay_ms

_BUFFER = bytearray(1)


def _fit(size: uint8) -> None:
    if len(_BUFFER) < 1 + size:
        _BUFFER.extend(bytes(1 + size - len(_BUFFER)))


class Field:
    def __init__(self, width: uint8) -> None:
        self.width = width
        _fit(width)


class Dev:
    bits = Field(2)


d = Dev()


def main():
    while True:
        _BUFFER[2] = 7
        print(_BUFFER[2])
        print("END")
        delay_ms(1200)
