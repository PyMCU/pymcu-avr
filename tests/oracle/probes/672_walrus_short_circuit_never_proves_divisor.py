# expect: match
# doc: https://docs.pymcu.org/limitations/#arithmetic
# Codex review of PyMCU golperf's proven-power-of-two mod rewrite (third
# round, case w2): n is declared 32, then a short-circuited `and` rebinds
# it to 4 only when flag is truthy, and `5 % n` is read afterwards. Two
# calls with different literal flags force f to stay a real, shared
# (non-inlined) function, so flag is a genuine runtime unknown inside the
# one compiled body. n has two textual writes, which refuses the rewrite
# outright; the earlier mechanism recorded n == 4 from lowering the right
# operand of `and` without joining it against the skipped path, so every
# call answered as if flag had been truthy. CPython: 5 1.
from pymcu.types import uint8, uint16


def f(flag: uint8) -> uint16:
    n: uint16 = 32
    flag and (n := 4)
    return 5 % n


print(f(0), f(1))
print("END")
