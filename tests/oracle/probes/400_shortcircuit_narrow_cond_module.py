# expect: match
# doc: https://github.com/PyMCU/PyMCU/issues/513
# `A or B` reaches B only when A did not hold, so `last is None or pos != last` proves
# `last` is live by the time the right operand reads it. #513 was that the proof never
# reached the operand, and it was TWO independent lowerings: the jump chain an `if`
# condition becomes, and VisitBinary for the value form. This is the condition half at
# module level; 401 is the same program as a value, and they must agree.
#
# `pos` REPEATS (0, 0, 2, 2) on purpose. With four distinct positions the answer is 4
# whether the guard works or folds to always-true, so the probe would assert nothing.
# Repeating them makes the correct answer 2 and a folded guard 4 or 0.
from pymcu.chips.atmega328p import GPIOR0

last = None
i = 0
total = 0
while i < 4:
    pos = (i // 2) * 2 + GPIOR0.value
    if last is None or pos != last:
        total = total + 1
        last = pos
    i = i + 1
print(total)
print("END")
