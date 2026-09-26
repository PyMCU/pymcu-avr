# expect: match
# doc: https://github.com/PyMCU/PyMCU/issues/513
# The value half of 400: the same short-circuit narrowing bound to a name first, which
# goes through VisitBinary instead of the condition's jump chain. The two lowerings are
# independent, so one can be fixed while the other stays broken; the pair is the point.
from pymcu.chips.atmega328p import GPIOR0

last = None
i = 0
total = 0
while i < 4:
    pos = (i // 2) * 2 + GPIOR0.value
    changed = last is None or pos != last
    if changed:
        total = total + 1
        last = pos
    i = i + 1
print(total)
print("END")
