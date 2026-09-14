# PyMCU -- list-param-mp-sevenseg-upstream: a MicroPython library compiled UNMODIFIED.
#
# src/sevenseg.py is vendored verbatim from
#   https://github.com/kritishmohapatra/micropython-sevenseg   (sevenseg.py, MIT, 2025)
# md5 6da7412cf575e804562c24616769e3a7. Do not edit it: the point of this fixture is that it
# is byte-identical to what a user downloads. It exercises, in one file, a comprehension of
# instances over a list of pin numbers (PyMCU#332), a constant subscript of that list
# (PyMCU#333), an optional peripheral guarded by `if self.dp:` with the field set to None
# (PyMCU#334), a dict literal in a field (PyMCU#335) whose values are lists (PyMCU#336), and
# zip over the field of pins against a row of that dict (PyMCU#337).
#
# Segments a..g on D2..D8, so a..f are PD2..PD7 and g is PB0, common cathode. The library
# loops a digit a second; here each digit prints the two port registers instead, which is the
# same measurement a scope makes and the only one an emulator can check: reading the pin back
# would fold to the value just written and pass on a firmware that drives nothing.
#
# Expected UART, PORTD & 0xFC then PORTB & 0x01, digits 0..9 then clear():
#   252 0 / 24 0 / 108 1 / 60 1 / 152 1 / 180 1 / 244 1 / 28 0 / 252 1 / 188 1 / 0 0
#   END
from sevenseg import SevenSeg
from pymcu.chips.atmega328p import PORTB, PORTD
from pymcu.hal.console import print

pins = [2, 3, 4, 5, 6, 7, 8]        # a..g on D2..D8
display = SevenSeg(pins, common_anode=False)


def main():
    for i in range(10):
        display.show(i)
        print(PORTD.value & 0xFC, PORTB.value & 0x01)
    display.clear()
    print(PORTD.value & 0xFC, PORTB.value & 0x01)
    print("END")


main()
