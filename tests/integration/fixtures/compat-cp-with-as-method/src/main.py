# PyMCU -- compat-cp-with-as-method: a method call on the name bound by `with ... as`
# (PyMCU#305).
#
# `with DigitalInOut(D6) as pin:` binds pin as a pure alias of the manager. Reading a field
# through it always worked; calling a method on it was resolved as a free function and
# refused as `call to undefined function 'pin_switch_to_output'`. This is the CircuitPython
# idiom, and it is the whole point of the `with` form.
#
# Inside the block D6 is an output driven high (DDRD bit 6 set, PORTD bit 6 set). On the way
# out __exit__ calls deinit(), which returns the pin to an input.
import board
import digitalio
from pymcu.chips.atmega328p import DDRD, PORTD
from pymcu.hal.console import print


def main():
    with digitalio.DigitalInOut(board.D6) as pin:
        pin.switch_to_output(True)
        print("in", DDRD.value & 0x40, PORTD.value & 0x40)
    print("out", DDRD.value & 0x40)
    print("END")
