# expect: match
# doc: https://docs.pymcu.org/limitations/#arithmetic
# Codex review of PyMCU golperf's proven-power-of-two mod rewrite (second
# round, case 5): x is reassigned on a conditional path that breaks out of
# a for loop, then read as a mod divisor. CPython: x ends at 4, 5 % x is 1.
from pymcu.types import uint16


def f(n: uint16) -> uint16:
    x: uint16 = (n & 0) + 32
    for i in range(n):
        if i == 0:
            x = 4
            break
    return 5 % x


print(f(1))
print(f(2))
print("END")
