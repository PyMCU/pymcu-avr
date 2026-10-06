# expect: match
# doc: https://docs.pymcu.org/limitations/#dynamic-memory-and-containers
# An int32, uint32 or float array element is four bytes. The backend's element load and store
# moved only a uint16's two, and scaled a run-time index by at most two: `buf[i] = v` kept the
# low byte of v and `buf[i]` read the rest from whatever the registers last held, so the
# global form printed the right value by accident and a field form printed 1004 & 0xFF = 236.
# Every container (module, function, field), both index kinds, `+=`, and an array past 256
# bytes, where the index is carried as a pair.
from pymcu.types import uint8, int32, uint32, inline
from pymcu.chips.atmega328p import GPIOR0

s = GPIOR0.value

g: int32[3] = [0] * 3
g[s] = s - 70000
g[s + 1] = s + 100000
g[s + 1] += s - 3
x: uint8 = s + 7
print(x, g[s], g[s + 1], g[1], g[-1])


def local() -> uint32:
    b: uint32[4] = [0] * 4
    b[s + 2] = s + 3000000000
    b[3] = b[s + 2] + 1
    return b[3] - b[s + 2] + b[2]


print(local())


class R:
    def __init__(self):
        self._buf: int32[3] = [0] * 3

    @inline
    def put(self, i: uint8, v: int32):
        self._buf[i] = v

    @inline
    def get(self, i: uint8) -> int32:
        return self._buf[i]

    def bump(self, i: uint8, v: int32):
        self._buf[i] += v


r = R()
r.put(s, s + 1004)
r.bump(s + 1, s - 2000000000)
print(r.get(s), r.get(s + 1), r._buf[2])

big: int32[80] = [0] * 80
big[s + 70] = s - 1234567
big[79] = s + 7654321
print(big[s + 70], big[s + 79], big[s])
print("END")
