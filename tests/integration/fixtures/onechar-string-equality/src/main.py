# PyMCU -- onechar-string-equality: a one-character string compared with itself.
#
# Regression for PyMCU#211. `x = "a"` then `if x == "a":` folded FALSE and the compiler deleted
# the branch that should have run. Not a wrong value printed -- code removed, with nothing in the
# build to notice.
#
# The cause is that one string arrived as two different numbers. A one-character literal in
# expression position lowers to its own character code (97); the same literal read back through a
# name is an interned id (256). The compile-time comparison compared the numbers, so "a" was not
# equal to "a". Two characters was always correct, because both sides took the interning path.
#
# Every case here is a compile-time fold, so what the firmware prints IS the decision the
# compiler made. Both lengths are present, and both directions of the operator: `!=` had the
# mirror-image failure and would pass a test that only checked `==`.
#
# Expected UART output:
#   eq1      x == "a"  with x = "a"     discriminator, folded FALSE before the fix
#   ne1      x != "a"  with x = "a"     discriminator, folded TRUE before the fix
#   eq2      x == "ab" with x = "ab"    control, always correct
#   ne2      x != "ab" with x = "ab"    control, always correct
#   diff     x == "b"  with x = "a"     control, correctly false before and after
#   num      "A" == 65                  control, the character-code reading is unchanged
#   in1      c in ("a", "b")            discriminator, folded FALSE before the fix
#   in2      c in ("aa", "bb")          control, always correct
#   mt1      match c: case "a"          discriminator, fell to the wildcard before the fix
#   mt2      match cc: case "aa"        control, always correct
from pymcu.hal.console import print


def main():
    a = "a"
    if a == "a":
        print("eq1")

    if a != "a":
        print("eq1-BROKEN")
    else:
        print("ne1")

    ab = "ab"
    if ab == "ab":
        print("eq2")

    if ab != "ab":
        print("eq2-BROKEN")
    else:
        print("ne2")

    if a == "b":
        print("diff-BROKEN")
    else:
        print("diff")

    # A one-character literal is still its character code where a number is what is meant.
    # This is what `uart.write('\n')` depends on, and it must not move.
    if "A" == 65:
        print("num")
    else:
        print("num-BROKEN")

    # `in` and `match` compare the same way and failed the same way: the character code on one
    # side, the interned id on the other. Two more sites, one mechanism.
    if a in ("a", "b"):
        print("in1")
    else:
        print("in1-BROKEN")

    if ab in ("ab", "cd"):
        print("in2")
    else:
        print("in2-BROKEN")

    match a:
        case "a":
            print("mt1")
        case _:
            print("mt1-BROKEN")

    match ab:
        case "ab":
            print("mt2")
        case _:
            print("mt2-BROKEN")
