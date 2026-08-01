# PyMCU -- delay-outline: a constant delay reached from several call sites is
# emitted once as a shared subroutine (__dly_c<loops>) instead of an 18-byte
# inline busy loop per site.
#
# Three delay_ms(1) calls put the loop count over the 2-site outlining threshold.
# The BREAK checkpoints bracket the first one so the test can verify the shared
# subroutine still spins for ~16 000 cycles at 16 MHz: it runs one iteration less
# than the inline form, because CALL+RET (8 cycles) stands in for that iteration
# (6 cycles), leaving the delay a cycle or two longer -- never shorter.
#
# Checkpoints:
#   1 -- immediately before the first delay_ms(1)
#   2 -- immediately after  the first delay_ms(1)
#
from pymcu.time import delay_ms
from pymcu.types import asm


def main():
    asm("BREAK")    # Checkpoint 1: record cycle counter here

    delay_ms(1)     # ~16 000 cycles at 16 MHz, via the shared subroutine

    asm("BREAK")    # Checkpoint 2: measure elapsed cycles

    delay_ms(1)
    delay_ms(1)

    while True:
        pass
