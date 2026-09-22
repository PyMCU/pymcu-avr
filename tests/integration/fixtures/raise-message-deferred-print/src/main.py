# raise-message-deferred-print: PyMCU/PyMCU#435.
#
# A non-literal raise message (f-string, a call inside one) compiles as a
# deferred print: runtime pieces are stored at the raise, and print(e) replays
# them. RFC 0005. The Adafruit shape is adafruit_pixelbuf /
#   raise ValueError(f"Expected tuple of length {self._bpp}, got {len(value)}")
# and the issue's minimal program is raise ValueError(f"bad address {addr}").
#
# WHAT DISCRIMINATES: the three messages CPython prints for the same program.
# A compile that still refused a call in a raise would not build. A compile
# that accepted the syntax and discarded the pieces would print empty lines.
# The third message also stores a float piece that arrives as a parameter
# behind a narrower one -- read wrong, it prints 0.0.
#
# The fourth raise is the adafruit_seesaw spelling: an f-string implicitly
# concatenated with a plain literal across a line break, with an :x piece.
# The last two carry format specs on float slots and an int under an f spec --
# print(e) must replay them through the same writers print(f"...") uses.
#
# Expected UART output, which is what CPython prints for the same program:
#   bad address 200
#   Expected tuple of length 3, got 0
#   code 7 temp 36.5
#   Seesaw hardware ID returned 0x60 is not correct! Please check your wiring.
#   bus voltage -0.1V under range
#   bus voltage -2.7V under range
#   count 9.00 pins
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


def check(code: uint8, temp: float) -> uint8:
    if code == 7:
        raise ValueError(f"code {code} temp {temp}")
    return code


def id_check(chip: uint8) -> uint8:
    if chip != 0x55:
        raise RuntimeError(
            f"Seesaw hardware ID returned 0x{chip:x} is not "
            "correct! Please check your wiring."
        )
    return chip


def volt_check(v: float) -> uint8:
    if v < 0.0:
        raise ValueError(f"bus voltage {v:.1f}V under range")
    return 0


def count_check(n: uint8) -> uint8:
    if n == 9:
        raise ValueError(f"count {n:.2f} pins")
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

        try:
            v3: uint8 = check(7, 36.5)
            print(v3)
        except ValueError as e:
            print(e)

        try:
            v4: uint8 = id_check(0x60)
            print(v4)
        except RuntimeError as e:
            print(e)

        try:
            volt_check(-0.05)
        except ValueError as e:
            print(e)

        try:
            volt_check(-2.675)
        except ValueError as e:
            print(e)

        try:
            v5: uint8 = count_check(9)
            print(v5)
        except ValueError as e:
            print(e)

        print("END")
        delay_ms(1200)
