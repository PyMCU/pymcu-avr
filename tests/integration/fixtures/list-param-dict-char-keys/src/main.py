# PyMCU -- list-param-dict-char-keys: a seven-segment table keyed by CHARACTERS (PyMCU#338).
#
# A one-character string literal folds to its character code, so these keys are constant
# integers that are simply not contiguous from zero. The table is read three ways: with a
# constant key, with the characters of a constant string, and with a byte only known at run
# time -- GPIOR0 here, a UART in real life.
#
# Segments a..g on D2..D8, so a..f are PD2..PD7 and g is PB0, common cathode. Each lookup
# prints the two port registers, which is the same measurement a scope makes and the only one
# an emulator can check: reading the pin back would fold to the value just written and pass on
# a firmware that drives nothing.
#
# Expected UART, with GPIOR0 seeded ord('b') = 98 and GPIOR1 seeded ord('X') = 88:
#   220 1          <- show("A"), a constant key
#   228 0          <- the characters of "Cb-": C, then b, then -
#   240 1
#   0 1
#   240 1          <- show(code) with code = GPIOR0, the same glyph as 'b'
#   X refused      <- a code with no glyph is the library's own raise, not a compiler refusal
#   END
from machine import Pin
from pymcu.chips.atmega328p import GPIOR0, GPIOR1, PORTB, PORTD
from pymcu.hal.console import print


class SevenSegChars:
    def __init__(self, pins):
        self.segments = [Pin(p, Pin.OUT) for p in pins]
        self.chars = {
            "0": [1, 1, 1, 1, 1, 1, 0],
            "1": [0, 1, 1, 0, 0, 0, 0],
            "A": [1, 1, 1, 0, 1, 1, 1],
            "b": [0, 0, 1, 1, 1, 1, 1],
            "C": [1, 0, 0, 1, 1, 1, 0],
            "-": [0, 0, 0, 0, 0, 0, 1],
            " ": [0, 0, 0, 0, 0, 0, 0],
        }

    def show(self, ch):
        if ch not in self.chars:
            raise ValueError("no glyph")
        pattern = self.chars[ch]
        for pin, on in zip(self.segments, pattern):
            pin.value(on)


d = SevenSegChars([2, 3, 4, 5, 6, 7, 8])


def report():
    print(PORTD.value & 0xFC, PORTB.value & 0x01)


def main():
    d.show("A")
    report()

    for ch in "Cb-":
        d.show(ch)
        report()

    code = GPIOR0.value
    d.show(code)
    report()

    # A CONSTANT key with no glyph is a compile-time KeyError, so the miss is exercised the
    # way it happens in a real program: a code that only run time knows. GPIOR1 is seeded with
    # 88 ('X'), which the table has no glyph for.
    other = GPIOR1.value
    try:
        d.show(other)
        report()
    except ValueError:
        print("X refused")

    print("END")


main()
