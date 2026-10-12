# expect: match
# doc: https://docs.pymcu.org/roadmap/#language
"""`not xs` is true exactly when the list is empty."""
from pymcu.types import uint8

empty: list[uint8] = list()
full: list[uint8] = list()
full.append(3)
if not empty:
    print("not empty: yes")
else:
    print("not empty: no")
if not full:
    print("not full: yes")
else:
    print("not full: no")
print("END")
