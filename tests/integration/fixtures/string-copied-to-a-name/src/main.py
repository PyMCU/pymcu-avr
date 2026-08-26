# PyMCU -- string-copied-to-a-name: binding a string to a second name keeps its text.
#
# Regression for PyMCU#209. `a = "abc"` then `b = a` then `print(b)` printed 256 -- the
# string's interned id, written as a decimal. Two lines, the most ordinary construct there is,
# clean build, no diagnostic. The number depends on how many other strings the program has,
# which is what makes it read as noise rather than as a bug.
#
# The assignment lowering recorded a name's text only when the right-hand side was spelled as a
# literal, and cleared it otherwise. That branch was doing two jobs: it also stopped the name
# selecting const[str] overloads, which IS load-bearing (#144), and must keep refusing when a
# name is rebound to a different text on another path (#145). Only the text half was wrong.
#
# Length-independent, unlike #211: one character and three characters failed identically, which
# is what says the id was recoverable all along and the boundary simply did not look.
#
# Expected UART output:
#   abc      b = a, three characters       discriminator, printed 256 before
#   x        e = d, one character          discriminator, printed 256 before
#   hi       c = BANNER, module constant   discriminator, printed 256 before
#   fld      n = c.name, LOCAL instance    discriminator, printed 256 before
#
# The instance is deliberately a LOCAL. Measured: the same copy through a MODULE-LEVEL instance
# already printed its text before this fix, so writing it that way would have looked like a
# discriminator and been a control.
#   abc      print(a) directly             control, always correct
#   done     terminator                    the last line is "abc" too, so the test needs a
#                                          distinct marker to know the program finished
#
# The join through a copied separator is NOT here. On the old compiler it refuses the whole
# program, which would hide every print above it -- the baseline would emit nothing and the
# fixture would look like it discriminated on all six when it discriminated on none. It is
# pinned in tests/stdlib/test_join_names_what_it_needs.py instead.
from pymcu.hal.console import print
from pymcu.types import inline

BANNER = "hi"


class Cfg:
    @inline
    def __init__(self):
        self.name: str = "fld"
        self.sep: str = "-"


def main():
    cfg = Cfg()

    a = "abc"
    b = a
    print(b)

    d = "x"
    e = d
    print(e)

    c = BANNER
    print(c)

    n = cfg.name
    print(n)

    print(a)
    print("done")
