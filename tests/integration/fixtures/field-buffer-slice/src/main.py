# field-buffer-slice: self._buffer[0:2] on a field bytearray.
#
# adafruit_sht4x writes
#   temp_data = self._buffer[0:2]
#   humidity_data = self._buffer[3:5]
# after a 6-byte I2C read. A named buf[0:2] already copied the window;
# a field is the same SRAM array.
#
# WHAT DISCRIMINATES: prints 10, 40. A compile that still required a
# VariableExpr slice target would refuse "Slice indexing is only
# supported on named fixed-size arrays".
from pymcu.types import uint8
from pymcu.time import delay_ms


class SHT:
    def __init__(self):
        self._buffer = bytearray([10, 20, 30, 40, 50, 60])

    def head(self) -> uint8:
        t = self._buffer[0:2]
        return t[0]

    def mid(self) -> uint8:
        t = self._buffer[3:5]
        return t[0]


s = SHT()


def main():
    while True:
        print(s.head())
        print(s.mid())
        print("END")
        delay_ms(1200)
