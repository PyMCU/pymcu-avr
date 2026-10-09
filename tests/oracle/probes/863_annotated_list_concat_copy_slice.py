# expect: match
# doc: https://docs.pymcu.org/roadmap/#language
from pymcu.types import uint8

src: list[uint8] = [1, 2, 3, 4, 5]
tail: list[uint8] = [9, 9]
cat: list[uint8] = src + tail
copy: list[uint8] = list(src)
piece: list[uint8] = src[1:3]
print(len(cat), len(copy), len(piece))
print(cat[0], cat[4], cat[5], cat[6])
print(copy[0], copy[4])
print(piece[0], piece[1])
copy[0] = 99
print(src[0], copy[0])
print("END")
