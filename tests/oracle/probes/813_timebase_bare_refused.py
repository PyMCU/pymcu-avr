# expect: refuse from pymcu.chips import __TIMEBASE__
# doc: https://docs.pymcu.org/limitations/
# `__TIMEBASE__` bare: same rule as __CHIP__ -- import it from pymcu.chips.
if __TIMEBASE__:
    x = 1
