# PyMCU -- dict-one-char-key-index: the LOUD half of PyMCU#215.
#
# `d[k]` with a one-character key held in a name did not return a wrong value, it refused to
# build: "KeyError: 257 is not a key of this dict literal", naming a number the program never
# wrote. It is the same compiler site as `d.get(k, default)` and differs only in whether a
# default was passed.
#
# It is its own fixture because a failed build stops everything after it. Sharing a program
# with the silent rows means the silent rows can never be measured, and the fixture then only
# ever demonstrates the half that was already obvious.
from pymcu.chips.atmega328p import GPIOR0
from pymcu.types import asm, uint8


def main():
    d = {"a": 70, "b": 7}
    k = "a"
    GPIOR0.value = uint8(d[k])
    asm("BREAK")
    while True:
        pass


main()
