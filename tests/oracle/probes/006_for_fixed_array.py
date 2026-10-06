# expect: match
# doc: https://docs.pymcu.org/roadmap/#language
from pymcu.types import uint8
xs: uint8[4] = [2, 4, 6, 8]
s = 0
for x in xs:
    s = s + x
print(s)
print("END")
