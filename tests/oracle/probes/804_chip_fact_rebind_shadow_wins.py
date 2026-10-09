# expect: match
# doc: https://docs.pymcu.org/roadmap/
# `from pymcu.chips import __FREQ__` then `__FREQ__ = 42`: the assignment
# rebinds the name, so the read answers 42 -- the user's binding wins over the
# compiler fact.
from pymcu.chips import __FREQ__

__FREQ__ = 42
print(__FREQ__)
print("END")
