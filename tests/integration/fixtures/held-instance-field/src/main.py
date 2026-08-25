# PyMCU -- held-instance-field: reading a field of a held instance after calling its method.
#
# Regression for PyMCU#119. An Outer that holds an Inner and reads Inner's field right after
# calling Inner's method printed 0, 0, 0. Two separate faults produced it: the caller kept
# folding the value the field held at construction, and the call itself passed the flattened
# field VALUES where the slot body expected the address of the instance, so the callee wrote
# its state through a null pointer.
#
# The third poll finds _state == 2, returns 0, and leaves _value alone, so the last line
# repeats 8 -- that is CPython's answer for this program, and the issue's "0" was a slip.
#
# Expected UART output:
#   7
#   8
#   8
#   done
from pymcu.hal.console import print
from pymcu.types import uint8, uint16


class Inner:
    def __init__(self):
        self._state: uint16 = 0
        self._value: uint16 = 0

    def poll(self) -> uint8:
        if self._state == 0:
            self._value = 7
            self._state = 1
            return 2
        if self._state == 1:
            self._value = 8
            self._state = 2
            return 2
        return 0


class Outer:
    def __init__(self):
        self._state: uint16 = 0
        self.inner: Inner = Inner()
        self._value: uint16 = 0

    def poll(self) -> uint8:
        r: uint8 = self.inner.poll()
        if r == 2:
            self._value = self.inner._value
            return 2
        return 0


def main():
    o = Outer()
    i: uint16 = 0
    while i < 3:
        r: uint8 = o.poll()
        print(o._value)
        i = i + 1

    print("done")
