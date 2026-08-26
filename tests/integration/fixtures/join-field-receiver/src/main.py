# PyMCU -- join-field-receiver: str.join on a separator that lives in a field.
#
# Regression for PyMCU#191. `o.sep.join([...])` was refused by a message telling the reader to
# assign the result to a variable, which the program already did. The real condition was the
# RECEIVER: StaticStringOf handled a literal and a bare name and never looked at a member access,
# so the assignment lowering declined and the expression fell through to a refusal describing a
# property the program already had.
#
# The SINGLE-LEVEL case is the one worth having here. The issue is titled "on a nested field", and
# a test set written from that title will not contain `o.sep` -- which was refused identically.
# The nesting was never the condition.
#
# Every line is a discriminator or a named control. The controls are not decoration: without them
# "a one-character field prints its char code" reads as a string bug rather than a field bug, and
# the fix aims at the wrong layer.
#
# All of it in assignment form. `print(o.sep.join([...]))` as a bare expression is a separate,
# genuinely unsupported shape and is still refused -- deliberately not covered here.
#
# Expected UART output:
#   x, y      single-level field receiver           discriminator, refused before the fix
#   x, y      nested field receiver, two levels     discriminator, refused before the fix
#   x,y       one-character field separator         discriminator, refused before the fix
#   x, y      plain __init__, not @inline           discriminator, refused before the fix
#   x,y       plain local receiver                  control, always compiled
#   x,y       literal receiver at the call site     control, always compiled
#   ,         one-character field, read directly    control for the char-code degradation
#   , .       multi-character field, read directly  control, always correct
from pymcu.hal.console import print
from pymcu.types import inline


class Held:
    @inline
    def __init__(self, s: str):
        self.sep: str = s


class Outer:
    @inline
    def __init__(self):
        self.inner: Held = Held(", ")


class Flat:
    @inline
    def __init__(self):
        self.sep: str = ", "


class Tight:
    @inline
    def __init__(self):
        self.sep: str = ","


class Plain:
    def __init__(self):
        self.sep: str = ", "


flat = Flat()
outer = Outer()
tight = Tight()
plain = Plain()


def main():
    a = flat.sep.join(["x", "y"])
    print(a)

    b = outer.inner.sep.join(["x", "y"])
    print(b)

    c = tight.sep.join(["x", "y"])
    print(c)

    d = plain.sep.join(["x", "y"])
    print(d)

    local = ","
    e = local.join(["x", "y"])
    print(e)

    f = ",".join(["x", "y"])
    print(f)

    print(tight.sep)
    print(flat.sep, ".", sep="")
