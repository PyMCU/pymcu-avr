# expect: match
# doc: https://docs.pymcu.org/roadmap/#language
"""`while xs:` runs while the list has elements; an empty list never enters the loop."""
from pymcu.types import uint8

empty: list[uint8] = list()
n: uint8 = 0
while empty:
    n = n + 1
    if n > 3:
        break
print(n)

xs: list[uint8] = list()
xs.append(1)
xs.append(2)
steps: uint8 = 0
while xs:
    steps = steps + 1
    if steps >= 2:
        break
print(steps)
print("END")
