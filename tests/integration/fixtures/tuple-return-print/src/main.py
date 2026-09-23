# PyMCU -- tuple-return-print: a `-> tuple` function's result prints as the tuple,
# through both a named binding and a direct call.
#
# Two independent defects hid the text behind the object pointer's low byte. The
# outlined function's `ret` marshaled by the DECLARED type, which for `-> tuple`
# stays UNKNOWN -- one byte -- so `make()` handed `r` a truncated pointer. And the
# sequence's element type never reached a module-level call site at all, because
# `main` compiles before outlined bodies: `funcListReturnElems` was still empty
# when both calls were lowered, so `print` saw a scalar and wrote the pointer's
# decimal. Scan-time `funcReturnSeqExprs` records the returned-name shape and the
# call site resolves the element type in its own context.
#
# Expected UART output (measured under CPython):
#   named: (11, 222)
#   direct: (11, 222)
#   field: 1.5
#   END
#
# The last line pins the sibling `sind fconst` fix: a float literal stored into a
# float field materialized nothing and kept whatever the registers held, so the
# read-back printed whatever was stale -- usually 0.0.

from pymcu.types import uint8, uint16

xs: list[uint16] = list()
xs.append(11)
xs.append(222)


def make() -> tuple:
    return tuple(xs)


class Box:
    def __init__(self) -> None:
        self.f: float = 1.5
        self.n: uint8 = 7


def main() -> None:
    r = make()
    print("named:", r)
    print("direct:", make())
    b = Box()
    print("field:", b.f)
    print("END")


main()
