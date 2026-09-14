# PyMCU -- list-param-dict-char-keys: a seven-segment table keyed by CHARACTERS (PyMCU#338).
#
# A one-character string literal folds to its character code, so these keys are constant
# integers that are simply not contiguous from zero. Two tables of the SAME glyphs, a row per
# character and a bit mask per character, each read three ways: with a constant key, with the
# characters of a constant string, and with a byte only known at run time -- GPIOR0 here, a
# UART in real life. Both shapes were refused, each for its own reason.
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
#   then the same six lines again, from the MASK table, which describes the same glyphs
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


class SevenSegMasks:
    """The same glyphs as a bit MASK per character (gfedcba) instead of a row.

    It is the other half of the same rule and it failed for its own reason: the table bound
    and a constant key folded, but the lookup refused every run-time key on the line after an
    `in` that had just compared the same run-time byte against the same keys.
    """

    def __init__(self, pins):
        self.segments = [Pin(p, Pin.OUT) for p in pins]
        self.chars = {"0": 0x3F, "1": 0x06, "A": 0x77, "b": 0x7C,
                      "C": 0x39, "-": 0x40, " ": 0x00}

    def show(self, ch):
        if ch not in self.chars:
            raise ValueError("no glyph")
        mask = self.chars[ch]
        for i in range(7):
            self.segments[i].value((mask >> i) & 1)


d = SevenSegChars([2, 3, 4, 5, 6, 7, 8])
m = SevenSegMasks([2, 3, 4, 5, 6, 7, 8])


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

    # The mask table, the same three ways in. Every line must match the rows above it: the
    # two tables describe the same glyphs.
    m.show("A")
    report()

    for ch in "Cb-":
        m.show(ch)
        report()

    m.show(code)
    report()

    try:
        m.show(other)
        report()
    except ValueError:
        print("X refused")

    print("END")


main()
