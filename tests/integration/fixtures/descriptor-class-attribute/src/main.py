# PyMCU -- descriptor-class-attribute: a register map declared the way every CircuitPython
# driver declares one, as class attributes whose class defines __get__ and __set__.
#
# PyMCU#268: a class-level attribute was reachable as `Cls.ATTR` and reported as
# "'Dev' has no attribute 'LIMIT'" as `inst.ATTR`, on a program CPython runs. The instance read
# built a flattened `inst_ATTR` that nothing registers and never asked the class.
#
# PyMCU#360: a class attribute whose class defines __get__ is a DESCRIPTOR, so `inst.attr` is
# `type(inst).attr.__get__(inst, type(inst))` and `inst.attr = v` is `__set__`. Only the
# explicit spelling compiled, and nobody writes it.
#
# Bases are exercised because class attributes do not inherit on their own: inheritance copies
# methods and leaves the attribute keyed under the base's own prefix, so `Sub.LIMIT` declared on
# `Dev` is only ever found by asking `Dev`.
#
# Every number is CPython's answer for the same lines, with buf = 00 12 34 56.
#
#   d.LIMIT      7     a class constant through the instance
#   d.flag       16    0x12 & 0x10, through __get__
#   d.other      4     0x34 & 0x04, through __get__
#   s.LIMIT      7     the constant found on the base
#   s.flag       16    the descriptor found on the base
#   d.flag = 0   -> buf[1] becomes 2      __set__ clearing a bit
#   d.other = 1  -> buf[2] stays 52       __set__ setting a bit already set
buf = bytearray([0x00, 0x12, 0x34, 0x56])


class Bit:
    def __init__(self, addr: int, mask: int) -> None:
        self.addr = addr
        self.mask = mask

    def __get__(self, obj, objtype=None) -> int:
        return buf[self.addr] & self.mask

    def __set__(self, obj, value: int) -> None:
        if value:
            buf[self.addr] = buf[self.addr] | self.mask
        else:
            buf[self.addr] = buf[self.addr] & ~self.mask


class Dev:
    LIMIT = 7
    flag = Bit(1, 0x10)
    other = Bit(2, 0x04)

    def __init__(self, addr: int) -> None:
        self.addr = addr


class Sub(Dev):
    pass


d = Dev(0x40)
s = Sub(0x41)


def main() -> None:
    print(d.LIMIT)
    print(d.flag)
    print(d.other)
    print(s.LIMIT)
    print(s.flag)
    d.flag = 0
    print(buf[1])
    d.other = 1
    print(buf[2])
    print("done")
