# expect: match
# doc: https://docs.pymcu.org/roadmap/#language
"""A list inside `and` / `or` is judged by its length, in a condition."""
from pymcu.types import uint8

empty: list[uint8] = list()
full: list[uint8] = list()
full.append(3)
flag: uint8 = 1

if empty and flag:
    print("A")
else:
    print("a")
if full and flag:
    print("B")
else:
    print("b")
if empty or flag:
    print("C")
else:
    print("c")
if empty or not flag:
    print("D")
else:
    print("d")
if full or empty:
    print("E")
else:
    print("e")
print("END")
