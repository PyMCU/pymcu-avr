# expect: match
# doc: https://docs.pymcu.org/roadmap/#language
# A buffer forwarded through a parameter and returned in a tuple is the same
# object as the caller's original: b is a, so b[0] = 9 is visible through a.
# Copying it home as if it were callee-local storage broke that identity.
from pymcu.types import uint8


def ident(buf: bytearray):
    return buf, 0


def outer() -> uint8:
    a = bytearray(1)
    a[0] = 7
    b, unused = ident(a)
    b[0] = 9
    return a[0]


print(outer())
print("END")
