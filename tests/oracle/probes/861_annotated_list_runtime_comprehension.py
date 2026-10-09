# expect: match
# doc: https://docs.pymcu.org/roadmap/#language
from pymcu.types import uint8

src: list[uint8] = [1, 2, 3, 4, 5]


def double(xs: list[uint8]) -> list[uint8]:
    doubled: list[uint8] = [x * 2 for x in xs]
    return doubled


result: list[uint8] = double(src)
print(len(result))
print(result[0], result[2], result[4])
print("END")
