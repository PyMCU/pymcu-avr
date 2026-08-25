from pymcu.types import uint16


class C:
    def __init__(self, v: uint16):
        self.v: uint16 = v


c = C(5)
n: uint16 = 7


def bump():
    c.v = c.v + 1


def get() -> uint16:
    return c.v


def plain() -> uint16:
    return n
