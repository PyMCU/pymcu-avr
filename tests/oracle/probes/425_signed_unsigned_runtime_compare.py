# expect: match
# doc: https://github.com/PyMCU/PyMCU/issues/525
# tracked: #525
# `a < b` with an int8 on the left and a RUNTIME uint8 on the right is evaluated as if the
# right operand were signed: 200 read as a signed byte is -56, and the emulator answers
# False where CPython answers True.
#
# The two directions are both here because they are what makes this irrefutable without
# CPython: `a < b` and `b > a` are the same question about the same two operands, and the
# emulator answers False to one and True to the other. No agreement about whose integer
# semantics are the reference is needed to call that wrong.
#
# The `if` below is the control, and it PASSES: the same comparison lowered as a condition
# is correct. Value position and condition position are two independent lowerings, the same
# split as #513, and only one of them has this.
#
# 041_signed_unsigned_compare is this probe's ancestor and is green, because it compares
# `a: int8 = -1` against a LITERAL `b: uint8 = 200`: the folder answers and the comparison
# it is named for never runs. The only change here is that b stops being foldable.
from pymcu.types import int8, uint8
from pymcu.chips.atmega328p import GPIOR0

a: int8 = -1
b: uint8 = 200 + GPIOR0.value

print(a < b)
print(b > a)
if a < b:
    print(1)
else:
    print(0)
print("END")
