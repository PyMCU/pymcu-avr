# expect: match
# doc: https://docs.pymcu.org/limitations/#arithmetic
# Codex review of PyMCU golperf's proven-power-of-two mod rewrite (second
# round, case 4): b.w is written through `alias.set4()`, alias being the
# same instance as b, and set4 is @outline (a real, non-inlined method).
# CPython: 1, 1 (w ends at 4 before either print). The rewrite may or may
# not prove the divisor through the alias -- what matters is that it never
# masks by the STALE value 32 (`& 31`) if it does not.
from pymcu.types import uint16, outline


class Box:
    def __init__(self):
        self.w: uint16 = 32
        self.a: uint16 = 0
        self.b: uint16 = 0

    @outline
    def set4(self):
        self.w = 4


def f(k: uint16) -> uint16:
    b = Box()
    alias = b
    alias.set4()
    return 5 % b.w


print(f(0))
print(f(1))
print("END")
