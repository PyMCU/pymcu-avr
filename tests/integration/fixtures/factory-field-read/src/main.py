# PyMCU -- factory-field-read: reading a field of an instance a function returned.
#
# Half of PyMCU#49. A single-field instance handed back by a factory IS its one field: the
# call returns that field's value in a register and the name is bound to it (RFC 0001 Model B
# handle). Calling a method on it already knew that, but reading the field directly did not
# and resolved to a per-field name nobody had written, so `o.a` came back as 0 while `o.g()`
# answered correctly on the very next line. The two reads are next to each other here for
# exactly that reason.
#
# GPIOR0 reads 0 out of reset, so the field holds 4 and g() answers 5.
#
# Expected UART output:
#   5
#   4
#   done
from pymcu.chips.atmega328p import GPIOR0
from pymcu.hal.console import print
from pymcu.types import uint8


class A:
    def __init__(self, a: uint8):
        self.a: uint8 = a

    def g(self) -> uint8:
        return self.a + 1


def make(v: uint8) -> A:
    return A(v)


def main():
    s: uint8 = GPIOR0.value + 4
    o = make(s)
    print(o.g())
    print(o.a)
    print("done")
