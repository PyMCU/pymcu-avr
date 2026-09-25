# expect: match
# doc: docs/language/limitations.md:773
from pymcu.types import uint8
a: uint8[5] = [1, 2, 3, 4, 5]
print(len(a))
print(len("hello"))
print(len(b"abc"))
print("END")
