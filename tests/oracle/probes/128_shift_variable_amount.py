# expect: match
# doc: https://docs.pymcu.org/roadmap/#language
from pymcu.types import uint8, int8
def shl(x: uint8, n: uint8) -> uint8:
    return x << n
def shr(x: uint8, n: uint8) -> uint8:
    return x >> n
def ashr(x: int8, n: uint8) -> int8:
    return x >> n
print(shl(1, 3))
print(shr(128, 3))
print(ashr(-8, 2))
print("END")
