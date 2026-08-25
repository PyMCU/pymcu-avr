# PyMCU -- const-or-branch: `and` / `or` between two comparisons decided at compile time.
#
# One of the eight combinations used to leave a jump to a label nobody defined:
# `X or Y` with X true and Y false, under an `if`. The left operand folds true and
# jumps over the rest, the right folds false and jumps to the caller's else label,
# and together they answer "statically true" -- so the caller keeps only the then
# branch and never defines that else label. The jump is unreachable, but an undefined
# label is `ld: undefined reference to L_2`, and only PYMCU_NO_OPT=1 showed it because
# the optimizer deletes the jump before the linker can miss the label.
#
# This fixture is in the differential corpus, so BUILDING it unoptimized is itself the
# regression test for the link failure. What the checkpoints add is the truth table:
# which branch each of the eight combinations actually takes, because a dead label is
# only half the risk -- the other half is folding the wrong way.
#
#   GPIOR0 bit k set  <- combination k took its THEN branch
#   GPIOR1 bit k set  <- combination k took its ELSE branch
#
#   bits 0..3: or(T,T) or(T,F) or(F,T) or(F,F)   -> 1 1 1 0  = 0x07
#   bits 4..7: and(T,T) and(T,F) and(F,T) and(F,F) -> 1 0 0 0 = 0x10
#   so GPIOR0 = 0x17, and GPIOR1 must be its exact complement, 0xE8: one branch each,
#   never both and never neither.
#
# Data-space addresses (ATmega328P): GPIOR0 = 0x3E, GPIOR1 = 0x4A, GPIOR2 = 0x4B
#
from pymcu.chips.atmega328p import GPIOR0, GPIOR1, GPIOR2
from pymcu.types import uint8, const, inline, asm


@inline
def arm(name: const) -> uint8:
    # The shape the bug was found in: a board number spelled alongside a port name.
    if name == "PD2" or name == 2:
        return 1
    elif name == "PD3" or name == 3:
        return 2
    return 0


def main():
    then_mask: uint8 = 0
    else_mask: uint8 = 0

    # --- or ---
    if 1 == 1 or 2 == 2:
        then_mask = then_mask | 0x01
    else:
        else_mask = else_mask | 0x01

    if 1 == 1 or 2 == 3:          # the combination that did not link
        then_mask = then_mask | 0x02
    else:
        else_mask = else_mask | 0x02

    if 1 == 2 or 2 == 2:
        then_mask = then_mask | 0x04
    else:
        else_mask = else_mask | 0x04

    if 1 == 2 or 2 == 3:
        then_mask = then_mask | 0x08
    else:
        else_mask = else_mask | 0x08

    # --- and ---
    if 1 == 1 and 2 == 2:
        then_mask = then_mask | 0x10
    else:
        else_mask = else_mask | 0x10

    if 1 == 1 and 2 == 3:
        then_mask = then_mask | 0x20
    else:
        else_mask = else_mask | 0x20

    if 1 == 2 and 2 == 2:
        then_mask = then_mask | 0x40
    else:
        else_mask = else_mask | 0x40

    if 1 == 2 and 2 == 3:
        then_mask = then_mask | 0x80
    else:
        else_mask = else_mask | 0x80

    GPIOR0.value = then_mask      # 0x17
    GPIOR1.value = else_mask      # 0xE8
    asm("BREAK")

    # --- the reported dispatch, all three arms ---
    GPIOR0.value = arm("PD2")     # 1, via the arm that used to dangle
    GPIOR1.value = arm(3)         # 2, via the elif
    GPIOR2.value = arm("PB0")     # 0, neither
    asm("BREAK")

    while True:
        pass
