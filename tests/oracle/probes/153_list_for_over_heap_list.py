# expect: match
# doc: https://docs.pymcu.org/roadmap/#language
from pymcu.types import uint8
xs: list[uint8] = list()
xs.append(2)
xs.append(3)
xs.append(4)
total = 0
for v in xs:
    total = total + v
print(total)
print("END")
