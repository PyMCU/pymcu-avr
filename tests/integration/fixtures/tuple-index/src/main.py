# PyMCU -- tuple-index: `f()[k]` on an inline multi-return call
#
# A subscript on a call whose callee returns several values reads the k-th
# result slot directly -- the tuple CPython would build is never materialised.
# This is the shape adafruit_tcs34725's `_temperature_and_lux_dn40()[0]` writes.
#
# Exercises:
#   - annotated `-> (uint8, uint8)` callee indexed at the call site
#   - unannotated callee: arity comes from the returns in its body
#   - a method call `self._method()[k]` on an instance
#   - Vec(3, 4)[1]: constructor result subscripted through __getitem__
#   - read_register(n)[k]: a call returning a fixed buffer, indexed
#
# opaque() is a real subroutine, so the seeds below are not compile-time
# constants and the arithmetic is genuinely evaluated at runtime.
#
# Output on UART (9600 baud):
#   "TI\n"      -- boot banner
#   "A:0B\n"    -- pair(10)[0] = 11
#   "B:0C\n"    -- pair(10)[1] = 12
#   "C:03\n"    -- tri(20)[2] = 3   (unannotated, branch A)
#   "D:05\n"    -- tri(5)[0] = 5    (unannotated, branch B)
#   "E:07\n"    -- tri(5)[2] = 7    (unannotated, branch B)
#   "F:2A\n"    -- sensor.read()[0] = 42   (method shape)
#   "G:2B\n"    -- sensor.read()[1] = 43
#   "H:04\n"    -- Vec(3, 4)[1] = 4        (ctor + __getitem__)
#   "I:09\n"    -- fetch()[1] = 9          (returned buffer)
#   "J:0A\n"    -- fetch()[2] = 10
#   "K=(0C,0D)\n" -- t = pair(11); print(t) writes the tuple text
#   "L:0C\n"    -- t[0] = 12               (bound-tuple index)
#   "M:02\n"    -- len(t) = 2
#   "N:19\n"    -- for x in t: sum = 25    (bound-tuple iteration)
#
from pymcu.types import uint8, inline
from pymcu.hal.uart import UART


def nibble_hi(val: uint8) -> uint8:
    n: uint8 = (val >> 4) & 0x0F
    if n < 10:
        return n + 48
    return n + 55


def nibble_lo(val: uint8) -> uint8:
    n: uint8 = val & 0x0F
    if n < 10:
        return n + 48
    return n + 55


def opaque(v: uint8) -> uint8:
    return v


@inline
def pair(a: uint8) -> (uint8, uint8):
    return (a + 1, a + 2)


@inline
def tri(x: uint8):
    if x > 10:
        return (1, 2, 3)
    return (x, x + 1, x + 2)


@inline
def fetch() -> uint8[4]:
    buf: uint8[4] = [opaque(8), 9, 10, 11]
    return buf


class Vec:
    x: uint8
    y: uint8

    @inline
    def __init__(self, x: uint8, y: uint8):
        self.x = x
        self.y = y

    @inline
    def __getitem__(self, i: uint8) -> uint8:
        if i == 0:
            return self.x
        return self.y


class Sensor:
    @inline
    def __init__(self):
        pass

    @inline
    def read(self):
        if opaque(1) > 100:
            return (0, 0)
        return (opaque(42), 43)


def report(uart: UART, tag: uint8, val: uint8):
    uart.write(tag)
    uart.write(':')
    uart.write(nibble_hi(val))
    uart.write(nibble_lo(val))
    uart.write('\n')


def main():
    uart = UART(9600)
    uart.println("TI")

    seed: uint8 = opaque(10)

    a: uint8 = pair(seed)[0]
    report(uart, 'A', a)

    b: uint8 = pair(seed)[1]
    report(uart, 'B', b)

    c: uint8 = tri(opaque(20))[2]
    report(uart, 'C', c)

    d: uint8 = tri(opaque(5))[0]
    report(uart, 'D', d)

    e: uint8 = tri(opaque(5))[2]
    report(uart, 'E', e)

    sensor = Sensor()
    f: uint8 = sensor.read()[0]
    report(uart, 'F', f)

    g: uint8 = sensor.read()[1]
    report(uart, 'G', g)

    h: uint8 = Vec(3, 4)[1]
    report(uart, 'H', h)

    i: uint8 = fetch()[1]
    report(uart, 'I', i)

    j: uint8 = fetch()[2]
    report(uart, 'J', j)

    t = pair(opaque(11))
    uart.write('K')
    uart.write('=')
    print(t)

    l: uint8 = t[0]
    report(uart, 'L', l)

    report(uart, 'M', len(t))

    total: uint8 = 0
    for x in t:
        total = total + x
    report(uart, 'N', total)


main()
