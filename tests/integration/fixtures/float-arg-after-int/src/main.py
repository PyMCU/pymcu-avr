# PyMCU -- float-arg-after-int: a float argument behind a narrower one must land
# in the register window the callee was compiled to read it from.
#
# Callers stage a float argument through R22:R25 and pop it into place. The pop
# targets were fixed: arg0 -> R22:R25, arg1 -> R18:R21. R18:R21 is arg1's window
# only when arg0 itself takes four bytes; behind a 1- or 2-byte argument the
# callee reads R20:R23, so the float's high half landed where the callee's low
# half was read and the rest in registers nobody read -- f(5, 36.5) saw 0.0.
# A float in position three was not staged at all and read as whatever the
# registers happened to hold. And a float argument whose parameter is never
# read in the callee's body has no entry in the size map at all -- the IR
# carries names, not widths -- so arg0's layout footprint was understated and
# the next float's window overlapped the registers arg0 really occupies.
#
# A green build proves nothing here, because it always built. Only the printed
# numbers do.
#
# Expected UART output, which is what CPython prints for the same program:
#   36.5
#   -1.25
#   7.5
#   done
from pymcu.hal.console import print
from pymcu.hal.uart import UART
from pymcu.types import uint8


def behind_int16(code: int, t: float) -> float:
    # arg0 is 2 bytes, so the float's window is R20:R23, not R18:R21.
    return t


def behind_two_u8(a: uint8, b: uint8, t: float) -> float:
    # The float sits at position 2: still a register window (R18:R21), but the
    # stash loop stopped at k <= 1, so it was never loaded at all.
    return t


def behind_float(k: float, t: float) -> float:
    # k is never read, so the size map holds no width for it and the layout used
    # to place t's window at R20:R23 -- overlapping the R22:R25 that a float
    # arg0 always occupies. The call-site width merge sizes it at 4 and t lands
    # at R18:R21, the window the callee reads.
    return t


uart = UART(9600)

print(behind_int16(5, 36.5))
print(behind_two_u8(1, 2, -1.25))
print(behind_float(1.5, 7.5))

print("done")

while True:
    pass
