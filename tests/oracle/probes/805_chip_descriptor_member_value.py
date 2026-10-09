# expect: match
# doc: https://docs.pymcu.org/roadmap/
# `__CHIP__.name` read as a value (not only inside an if) folds to the chip
# name the project targets.
from pymcu.chips import __CHIP__

v = __CHIP__.name
print(v)
print("END")
