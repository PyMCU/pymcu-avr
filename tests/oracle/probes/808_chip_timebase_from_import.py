# expect: compile
# doc: https://docs.pymcu.org/roadmap/
# `from pymcu.chips import __TIMEBASE__` binds whether the program runs the
# millisecond time base.
from pymcu.chips import __TIMEBASE__

if __TIMEBASE__ == 0:
    x = 1
