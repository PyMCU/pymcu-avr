# expect: match
# doc: https://docs.pymcu.org/limitations/#arithmetic
# Codex review of PyMCU golperf's proven-power-of-two mod rewrite (third
# round, case w1): x is declared 32, then reassigned to 4 right before a
# raise, and read back as a mod divisor in the except handler that catches
# it. x has TWO textual writes in its own source, which refuses the whole-
# program single-write criterion outright -- the earlier (second-round)
# mechanism read localConstantValues at this point and answered the
# pre-try value (32) instead of the one actually live in the handler (4).
# CPython: 1.
from pymcu.types import uint16


def f() -> uint16:
    x: uint16 = 32
    try:
        x = 4
        raise ValueError("x")
    except ValueError:
        return 5 % x
    return 0


print(f())
print("END")
