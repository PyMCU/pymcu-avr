# PyMCU -- mega-ramstart: absolute slot/array accesses must be based on the
# chip's RAMSTART, not a hardcoded 0x0100.
#
# The ATmega2560's RAM starts at 0x0200 (0x0000-0x01FF is register and
# extended-I/O space). CompileArrayLoad/CompileArrayStore/LoadIntoReg/
# StoreRegInto emitted `0x0100 + offset` for every access beyond the
# Y+63 displacement window, which is only right on parts whose RAMSTART is
# 0x0100. On this chip each of those accesses lands 0x100 low, in I/O space:
# a store is lost to a peripheral register and the matching load answers
# whatever the register holds, not what was written.
#
# DATA spans the boundary on purpose: elements 0-63 go through STD Y+q
# (always right), elements 64-99 through the absolute path (the bug).
#
# Expected UART output:
#   0
#   done
from pymcu.hal.console import print
from pymcu.types import uint8

DATA: uint8[100] = bytearray(100)


def main() -> None:
    for i in range(100):
        DATA[i] = 0x5A
    bad: uint8 = 0
    for i in range(64, 100):
        if DATA[i] != 0x5A:
            bad = bad + 1
    print(bad)
    print("done")


main()
