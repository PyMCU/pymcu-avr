# expect: match
# doc: https://docs.pymcu.org/roadmap/#language
"""The truth of a list also decides a `not` inside a conditional expression and an `and` / `or`
inside a loop."""
from pymcu.types import uint8

empty: list[uint8] = list()
full: list[uint8] = list()
full.append(4)
a: uint8 = 1 if not empty else 0
b: uint8 = 1 if not full else 0
print(a)
print(b)
n: uint8 = 0
for i in range(3):
    if empty or n:
        n = n + 10
    if full and not empty:
        n = n + 1
print(n)
print("END")
