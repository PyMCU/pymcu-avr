# expect: match
# doc: https://docs.pymcu.org/limitations/#compile-time-sequences-in-classes
# An uncaught raise ends an unrolled tuple/list for-loop at an earlier iteration than the
# last, the same as a break -- CPython leaves the name bound to whatever that iteration's
# own unrolled copy was processing. A raise has no literal `break`/`continue` keyword, so
# the old break-only check never materialized a real per-iteration write for it: a read in
# the exception handler saw the tuple's last element, 2, instead of the one in progress
# when the exception was raised, 1.
from pymcu.types import uint8

v: uint8 = 9
try:
    for v in (1, 2):
        raise ValueError()
except ValueError:
    print(v)
print("END")
