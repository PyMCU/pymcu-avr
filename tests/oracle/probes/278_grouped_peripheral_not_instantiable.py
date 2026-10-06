# expect: refuse not a class to instantiate
# doc: https://docs.pymcu.org/roadmap/
from pymcu.types import ptr, uint8, const

BASE: const[uint8] = 0x04


# RFC 0012: a register group is a namespace over the silicon, not a type. It has no
# fields, no constructor and no instance, so calling it means nothing and is refused
# with a located message instead of compiling to nothing.
class BLOCK:
    CTRL: ptr[uint8] = ptr(BASE * 256)

    RUN: int = 0


b = BLOCK()
b.CTRL.value = 1
print("END")
