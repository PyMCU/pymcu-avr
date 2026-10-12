# expect: match
# doc: https://docs.pymcu.org/roadmap/#language
"""adafruit_onewire's crc8: a static method that walks the bytes of any buffer it is given, called
with a module buffer and with a field of an instance."""
from pymcu.types import uint8


class Bus:
    def __init__(self):
        self.buf = bytearray(4)

    @staticmethod
    def crc8(data: bytearray) -> uint8:
        crc: uint8 = 0
        for byte in data:
            crc ^= byte
            for _ in range(8):
                if crc & 0x01:
                    crc = (crc >> 1) ^ 0x8C
                else:
                    crc >>= 1
                crc &= 0xFF
        return crc

    def check(self) -> uint8:
        self.buf[0] = 0x28
        self.buf[1] = 0xFF
        self.buf[2] = 0x64
        self.buf[3] = 0x1E
        return Bus.crc8(self.buf)


rom = bytearray(3)
rom[0] = 1
rom[1] = 2
rom[2] = 3
bus = Bus()
print(Bus.crc8(rom))
print(bus.check())
print("END")
