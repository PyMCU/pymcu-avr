# PyMCU -- constant-retired-by-a-load: a name that held a compile-time constant and is then
# assigned an array element must stop being that constant.
#
# Regression for PyMCU#359. PropagateCopies decided which instructions retire a tracked
# constant from a hand-written list -- Copy, AugAssign, Binary, Unary, Bitcast, InlineAsm,
# Call -- and every load was outside it. `acc = 0` then `acc = buf[2]` kept the 0, and every
# later read of acc folded to it. The store was correct; only the bookkeeping was not, so
# nothing was reported and PYMCU_NO_OPT=1 printed the right answer.
#
# The identity fold is how a driver reaches it: `0 << 8` is 0 and `0 | x` folds to x, so the
# first iteration of `reg = (reg << 8) | buf[i]` IS the two lines above, and a sensor read the
# value its accumulator was seeded with.
#
# Every number is CPython's answer for the same lines, with buf[1] = 0x12 and buf[2] = 0x34.
#
#   acc = 0 then acc = buf[2]                  52    measured 0
#   acc = 0 then acc = acc | buf[2]            52    measured 0
#   acc = 0 then acc = buf[2] | acc            52    measured 0
#   acc = 0 then acc = acc ^ buf[2]            52    measured 0
#   the same OR twice, over both bytes         54    measured 0
#   the value stored back into the array       52    measured 0
#   acc = 0 then acc = acc + buf[2]            52    correct before the fix, control
#   acc = 1 then acc = acc | buf[2]            53    correct before the fix, control
#
# The last two are controls: `+` was lowered through a temporary and a copy the old list did
# cover, and a non-zero seed leaves no identity to fold. A fix that traded one for the other
# would show here.
buf = bytearray([0x00, 0x12, 0x34, 0x56])

plain = 0
plain = buf[2]

ored = 0
ored = ored | buf[2]

ored_right = 0
ored_right = buf[2] | ored_right

xored = 0
xored = xored ^ buf[2]

both = 0
both = both | buf[2]
both = both | buf[1]

stored = 0
stored = stored | buf[2]
buf[0] = stored

added = 0
added = added + buf[2]

seeded = 1
seeded = seeded | buf[2]


def main() -> None:
    print(plain)
    print(ored)
    print(ored_right)
    print(xored)
    print(both)
    print(buf[0])
    print(added)
    print(seeded)
    print("done")
