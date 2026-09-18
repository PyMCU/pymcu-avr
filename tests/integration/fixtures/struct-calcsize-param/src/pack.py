# Adafruit adafruit_register.i2c_struct_array.StructArray shape: a class-body
# descriptor constructor calls _fit(struct.calcsize(struct_format)) with
# struct_format: str.
from pymcu.types import uint8
import struct

_BUFFER = bytearray(1)


def _fit(size: uint8) -> None:
    if len(_BUFFER) < 1 + size:
        _BUFFER.extend(bytes(1 + size - len(_BUFFER)))


class Field:
    def __init__(self, register_address: uint8, struct_format: str, count: uint8) -> None:
        self.format = struct_format
        self.address = register_address
        self.count = count
        _fit(struct.calcsize(struct_format))
