# expect: match
# doc: https://docs.pymcu.org/limitations/#compile-time-sequences-in-classes
# An uncaught raise ends an unrolled range for-loop at an earlier iteration than the last,
# the same as a break -- CPython leaves the name bound to whatever value the raising
# iteration was running with. The range unroll path's own break detection looked only for
# a literal `break`/`continue`, so a raise with no such keyword fell through to the
# guaranteed-complete case and read range(2)'s element-not-visited value, 2, instead of
# the one in progress when the exception fired, 0.
from pymcu.types import uint8

v: uint8 = 9
try:
    for v in range(2):
        raise ValueError()
except ValueError:
    print(v)
print("END")
