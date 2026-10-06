# expect: match
# doc: https://docs.pymcu.org/roadmap/#language
from pymcu.types import int8, uint8
a: int8 = -1
b: uint8 = 200
print(a < b)
print(b > a)
print("END")
