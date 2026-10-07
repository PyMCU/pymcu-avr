# expect: match
# doc: https://docs.pymcu.org/limitations/#arithmetic
# Codex review of PyMCU golperf's proven-power-of-two mod rewrite (second
# round, case 6): x is set right before a raise, read back as a mod
# divisor in the except handler that catches it. CPython: x is 4, 5 % x
# is 1.
from pymcu.types import uint16


def f(n: uint16) -> uint16:
    x: uint16 = (n & 0) + 32
    try:
        x = 4
        raise ValueError("x")
    except ValueError:
        return 5 % x
    return 0


print(f(1))
print(f(2))
print("END")
