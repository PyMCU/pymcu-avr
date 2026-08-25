# PyMCU -- folded-condition: a constant expression used bare as a condition.
#
# Regression for PyMCU#137. `if 3 & 1:` took the ELSE branch, and so did every other folded
# binary operation used bare as a condition: 3 + 1, 3 | 4, 3 << 1, 3 * 2. The conditional-jump
# lowering decides a constant-versus-constant condition with a switch that covers the six
# COMPARISON operators and nothing else, so any other operator fell through every case and
# kept the initial `false`.
#
# `if 5:` was always right, because a bare literal is not a binary expression at all, and
# `if x & 1:` was right too because x is not constant. That narrow gap is why it survived: it
# needs BOTH operands constant AND a non-comparison operator.
#
# What it cost in practice: the LCD driver writes its 4-bit init handshake as four nibbles of
# that shape, so the display never received the datasheet sequence 3, 3, 3, 2 -- it got
# 0, 0, 0, 0 -- and every LCD program was working by accident of what came after.
#
# The comparisons are here as the control: they always worked and must keep working, since the
# fix narrows exactly the branch that decides them.
#
# Expected UART output:
#   and
#   add
#   or
#   shift
#   mul
#   lt
#   eq
#   done
from pymcu.hal.console import print


def main():
    if 3 & 1:
        print("and")

    if 3 + 1:
        print("add")

    if 3 | 4:
        print("or")

    if 3 << 1:
        print("shift")

    if 3 * 2:
        print("mul")

    if 1 < 2:
        print("lt")

    if 2 == 2:
        print("eq")

    if 3 & 4:
        print("and-zero SHOULD NOT PRINT")

    if 1 > 2:
        print("gt SHOULD NOT PRINT")

    print("done")
