# expect: match
# doc: https://docs.pymcu.org/roadmap/
# A name reuse in another module's function must not touch the entry module's
# binding: print() pulls in uart_write_str, which keeps a local `b`, and its
# write used to run a bare-name sweep that unmarked the program's own
# `b = Pair(...)`. The `b.a` read afterwards then found no class at all.
# CPython: 1.
from pymcu.types import uint8


class Pair:
    def __init__(self, a: uint8, b: uint8) -> None:
        self.a = a
        self.b = b


b = Pair(1, 2)


def use() -> uint8:
    return b.a


print("warmup")
print(use())
print("END")
