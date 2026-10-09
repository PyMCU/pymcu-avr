# expect: compile
# doc: https://docs.pymcu.org/roadmap/
# `from pymcu.chips import F_CPU` -- the classic AVR spelling binds the same
# frequency fact __FREQ__ does.
from pymcu.chips import F_CPU

if F_CPU == 16000000:
    x = 1
