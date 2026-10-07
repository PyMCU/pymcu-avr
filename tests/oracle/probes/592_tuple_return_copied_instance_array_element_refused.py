# expect: refuse Pair instance stored
# doc: https://docs.pymcu.org/limitations/
# xs: Pair[1] is a callee-local instance array the tuple unpack copies home.
# The copy landed only the bytes: a Cls[N] also lives in arraysWithVariableIndex
# and moduleSramArrays, so EmitSequenceCopy took the SRAM branch and returned
# before instanceArrayClass propagated. a[0] then read the slot's first byte as
# a scalar (False) where CPython holds the object -- the alias kept the element
# class, so the refusal this names is what the copy must preserve.
from pymcu.types import uint8


class Pair:
    def __init__(self, a: uint8, b: uint8) -> None:
        self.a = a
        self.b = b


def f():
    xs: Pair[1] = [Pair(9, 9)]
    xs[0] = Pair(0, 7)
    return xs, 0


a, x = f()
print(bool(a[0]))
print("END")
