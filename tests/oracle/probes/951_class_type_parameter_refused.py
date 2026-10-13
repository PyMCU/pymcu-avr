# expect: refuse :6:10: error: SyntaxError: PEP 695 type parameters are not supported
# doc: https://github.com/PyMCU/PyMCU/issues/521
from pymcu.types import uint8


class Box[T]:
    def __init__(self, v: uint8):
        self.v = v


b = Box(4)
print(b.v)
print("END")
