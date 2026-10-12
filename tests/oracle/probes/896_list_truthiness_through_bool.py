# expect: match
# doc: https://docs.pymcu.org/roadmap/#language
"""bool(xs) is the truth of the list: False when it is empty."""
from pymcu.types import uint8

empty: list[uint8] = list()
full: list[uint8] = list()
full.append(3)
print(bool(empty))
print(bool(full))
print("END")
