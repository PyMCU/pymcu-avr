# raise-message-deferred-print: PyMCU/PyMCU#435.
#
# A non-literal raise message (f-string, a call inside one) compiles as a
# deferred print: runtime pieces are stored at the raise, and print(e) replays
# them. RFC 0005. The Adafruit shape is adafruit_pixelbuf /
#   raise ValueError(f"Expected tuple of length {self._bpp}, got {len(value)}")
# and the issue's minimal program is raise ValueError(f"bad address {addr}").
#
# WHAT DISCRIMINATES: the two messages CPython prints for the same program.
# A compile that still refused a call in a raise would not build. A compile
# that accepted the syntax and discarded the pieces would print empty lines.
#
# Expected UART output, which is what CPython prints for the same program:
#   bad address 200
#   Expected tuple of length 3, got 0
#   END
from pymcu.types import uint8
from pymcu.time import delay_ms


def read(addr: uint8) -> uint8:
    if addr > 127:
        raise ValueError(f"bad address {addr}")
    return addr


def length_of(n: uint8) -> uint8:
    xs: uint8[3] = [1, 2, 3]
    if n == 0:
        raise ValueError(f"Expected tuple of length {len(xs)}, got {n}")
    return n


def main():
    while True:
        try:
            v: uint8 = read(200)
            print(v)
        except ValueError as e:
            print(e)

        try:
            v2: uint8 = length_of(0)
            print(v2)
        except ValueError as e:
            print(e)

        print("END")
        delay_ms(1200)
