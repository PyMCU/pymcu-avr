# PyMCU -- yield-from: a generator delegating to another with `yield from`.
#
# The form was rejected by the parser. It is expanded rather than nested: the delegate's body
# is spliced in place with its locals renamed, BEFORE the state split, so one flat state
# machine comes out and the generator lowering does not change. Holding the delegate as a
# field would need a nested state machine to poll, which is a different piece of work.
#
# The four shapes here are the ones that decide whether the expansion is real:
#   simple        plain delegation
#   around        a yield before and after, and a local `i` that collides with the delegate's
#   in a loop     the delegation re-arms on the next pass
#   two levels    a generator that delegates to one that delegates
#
# Expected UART output:
#   0 1 2
#   100 0 1 2 100
#   0 1 0 1
#   7 8
#   done
from pymcu.hal.console import print
from pymcu.types import uint8


def cuenta():
    i: uint8 = 0
    while i < 3:
        yield i
        i = i + 1


def dos():
    j: uint8 = 0
    while j < 2:
        yield j
        j = j + 1


def simple():
    yield from cuenta()


def alrededor():
    i: uint8 = 100
    yield i
    yield from cuenta()
    yield i


def en_bucle():
    k: uint8 = 0
    while k < 2:
        yield from dos()
        k = k + 1


def hoja():
    yield 7
    yield 8


def medio():
    yield from hoja()


def dos_niveles():
    yield from medio()


def main():
    for a in simple():
        print(a)
    print("-")

    for b in alrededor():
        print(b)
    print("-")

    for c in en_bucle():
        print(c)
    print("-")

    for d in dos_niveles():
        print(d)

    print("done")
