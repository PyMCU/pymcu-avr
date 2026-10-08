# expect: match
# doc: https://docs.pymcu.org/limitations/#arithmetic
# Codex review of PyMCU golperf's proven-power-of-two mod rewrite (third
# round, case w3): n is declared 32, read as a mod divisor inside a while
# loop, then rewritten to 4 by a walrus on every iteration. n has two
# textual writes (the declaration and the walrus), which refuses the
# rewrite for every iteration, not just the first -- the earlier
# mechanism proved n from the loop's ENTRY value and answered that for
# both iterations, instead of 32 on the first and 4 on the second.
# CPython: 51 (iteration 1: total = 0*10 + 5 % 32 = 5; iteration 2:
# total = 5*10 + 5 % 4 = 51 -- the earlier mechanism answered 55, using
# n == 32 for both iterations instead of just the first).
from pymcu.types import uint8, uint16


def f() -> uint16:
    n: uint16 = 32
    i: uint8 = 0
    total: uint16 = 0
    while i < 2:
        total = total * 10 + 5 % n
        (n := 4)
        i = i + 1
    return total


print(f())
print("END")
