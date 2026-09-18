# bytearray-size-from-field: adafruit_74hc595's
#   self._number_of_shift_registers = n
#   self._gpio = bytearray(self._number_of_shift_registers)
# The field holds a compile-time n, so the buffer has a compile-time size.
#
# WHAT DISCRIMINATES:
#   7  -- first (only) byte of a 1-byte buffer
from pymcu.time import delay_ms


class Shift:
    def __init__(self, n: int):
        self._number_of_shift_registers = n
        self._gpio = bytearray(self._number_of_shift_registers)
        self._gpio[0] = 7

    def get(self) -> int:
        return self._gpio[0]


def main():
    while True:
        s = Shift(1)
        print(s.get())
        print("END")
        delay_ms(1200)
