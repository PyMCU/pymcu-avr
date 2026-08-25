# PyMCU -- loop-else: `for ... else` / `while ... else` on real silicon semantics.
#
# The else clause of a loop runs ONLY when the loop finished without executing a
# break. Every checkpoint below stores its answer in GPIOR1 and halts, so the
# suite can read the value out of the simulator with no UART.
#
# The loop bound is read back out of GPIOR0 rather than written as a literal, so
# the comparison is a run-time test and the loop cannot be folded away: both
# arms have to be generated for the checkpoint to mean anything.
#
# Data-space addresses (ATmega328P): GPIOR0 = 0x3E, GPIOR1 = 0x4A, GPIOR2 = 0x4B
#
# Checkpoints:
#   1. for/else, break taken          -> GPIOR1 = 1   (else must NOT run)
#   2. for/else, break not taken      -> GPIOR1 = 2   (else runs)
#   3. while/else, break taken        -> GPIOR1 = 1
#   4. while/else, break not taken    -> GPIOR1 = 2
#   5. inner break, outer else        -> GPIOR1 = 7   (a nested loop owns its break)
#   6. continue only, no break        -> GPIOR1 = 9   (continue is not a break)
#   7. break inside try/finally       -> GPIOR1 = 1, GPIOR2 = 3 (finally still runs)
#
from pymcu.chips.atmega328p import GPIOR0, GPIOR1, GPIOR2
from pymcu.types import uint8, asm


def main():
    # --- Checkpoint 1: for/else where the break IS taken ---
    GPIOR0.value = 3
    seed: uint8 = GPIOR0.value
    r1: uint8 = 0
    for i in range(5):
        if i == seed:
            r1 = 1
            break
    else:
        r1 = 2
    GPIOR1.value = r1
    asm("BREAK")

    # --- Checkpoint 2: the same loop, this time with no match and no break ---
    GPIOR0.value = 9
    seed2: uint8 = GPIOR0.value
    r2: uint8 = 0
    for j in range(5):
        if j == seed2:
            r2 = 1
            break
    else:
        r2 = 2
    GPIOR1.value = r2
    asm("BREAK")

    # --- Checkpoint 3: while/else where the break IS taken ---
    GPIOR0.value = 2
    seed3: uint8 = GPIOR0.value
    k: uint8 = 0
    r3: uint8 = 0
    while k < 5:
        if k == seed3:
            r3 = 1
            break
        k += 1
    else:
        r3 = 2
    GPIOR1.value = r3
    asm("BREAK")

    # --- Checkpoint 4: while/else that runs to the end of its condition ---
    GPIOR0.value = 9
    seed4: uint8 = GPIOR0.value
    m: uint8 = 0
    r4: uint8 = 0
    while m < 5:
        if m == seed4:
            r4 = 1
            break
        m += 1
    else:
        r4 = 2
    GPIOR1.value = r4
    asm("BREAK")

    # --- Checkpoint 5: the inner loop's break belongs to the inner loop ---
    GPIOR0.value = 1
    seed5: uint8 = GPIOR0.value
    r5: uint8 = 0
    for a in range(3):
        for b in range(3):
            if b == seed5:
                break
    else:
        r5 = 7
    GPIOR1.value = r5
    asm("BREAK")

    # --- Checkpoint 6: continue is not a break, so the else still runs ---
    GPIOR0.value = 1
    seed6: uint8 = GPIOR0.value
    r6: uint8 = 0
    for c in range(4):
        if c == seed6:
            continue
    else:
        r6 = 9
    GPIOR1.value = r6
    asm("BREAK")

    # --- Checkpoint 7: a break out of a try still skips the else, and the
    #     finally block still runs on the way out ---
    GPIOR0.value = 3
    seed7: uint8 = GPIOR0.value
    r7: uint8 = 0
    fin: uint8 = 0
    for d in range(5):
        try:
            if d == seed7:
                r7 = 1
                break
        finally:
            fin += 1
    else:
        r7 = 2
    GPIOR1.value = r7
    GPIOR2.value = fin
    asm("BREAK")

    while True:
        pass
