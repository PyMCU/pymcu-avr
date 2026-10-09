# expect: match
# doc: https://docs.pymcu.org/roadmap/#language
from pymcu.types import uint8


def make() -> list[uint8]:
    r: list[uint8] = [11, 22, 33]
    return r


src: list[uint8] = [1, 2, 3, 4, 5]
alias: list[uint8] = src
fromfn: list[uint8] = make()
print(len(alias), len(fromfn))
print(alias[0], alias[4])
print(fromfn[0], fromfn[2])
print("END")
