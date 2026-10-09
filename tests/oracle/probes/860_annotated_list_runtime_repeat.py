# expect: match
# doc: https://docs.pymcu.org/roadmap/#language
from pymcu.types import uint8, uint16

n: uint16 = 5
junk: list[uint8] = [7] * n
print(len(junk))
print(junk[0], junk[4])
junk[2] = 42
print(junk[2])
print("END")
