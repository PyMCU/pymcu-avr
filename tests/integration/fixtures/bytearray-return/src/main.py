# bytearray-return: PyMCU/PyMCU#464.
#
# adafruit_bmp280's _read_register builds a bytearray(length), fills it, and
# returns it. Callers then index the result. A buffer is element storage under
# a name; there is no handle to put in a register, so `return buf` was refused.
#
# Force-inlining the callee lays the buffer out in the caller's frame. WHAT
# DISCRIMINATES: 208 (0xD0) and 209 (0xD1), the two filled bytes. A lowering
# that still returned the array as a scalar would not index them.
#
# Expected UART output:
#   208
#   209
#   END
from pymcu.types import uint8


def read_reg(n: uint8) -> bytearray:
    buf = bytearray(2)
    buf[0] = n
    buf[1] = n + 1
    return buf


def main() -> None:
    b = read_reg(0xD0)
    print(b[0])
    print(b[1])
    print("END")
    while True:
        pass
