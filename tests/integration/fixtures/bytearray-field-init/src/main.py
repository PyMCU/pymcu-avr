# PyMCU -- bytearray-field-init: bytearray() assigned to a FIELD inside __init__ (PyMCU#392).
#
# `self.data = bytearray(...)` inside __init__ reached only the generic expression visitor,
# which has no lowering for the bytearray() builtin and refused with "a Python builtin that
# PyMCU does not provide" -- true of no import adding it, false of what already worked one
# binding away (`buf = bytearray(N); self.data = buf`, and a bytearray bound to a local
# first then stored into a field, both pre-existing).
#
# Expected UART output (CPython: b"abcd"[1] is 'b' == 98, b"abcd"[2] is 'c' == 99, and
# b"abcd"[1:3] reprs as the two-byte slice):
#   98
#   99
#   bytearray(b'bc')
#   done
from pymcu.hal.console import print
from pymcu.hal.uart import UART


class Buf:
    def __init__(self):
        self.data = bytearray(b"abcd")

    def __len__(self):
        return 4

    def __getitem__(self, i):
        return self.data[i]


uart = UART(9600)
b = Buf()
print(b[1])
print(b[2])
print(b[1:3])
print("done")

while True:
    pass
