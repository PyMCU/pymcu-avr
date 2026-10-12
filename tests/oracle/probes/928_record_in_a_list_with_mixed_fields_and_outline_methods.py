# expect: match
# doc: https://docs.pymcu.org/limitations/#iterators-and-comprehensions
# A record read from a list with uint8, uint16, float and bool fields, and @outline methods called on
# it by index and through a name, which take the record pointer as self.
from pymcu.types import uint8, uint16, outline


class Sample:
    def __init__(self, id: uint8, mv: uint16, scale: float, ok: bool) -> None:
        self.id = id
        self.mv = mv
        self.scale = scale
        self.ok = ok

    @outline
    def bump(self, by: uint16) -> None:
        self.mv = self.mv + by

    @outline
    def scaled(self) -> float:
        return self.mv * self.scale


class Rig:
    def gather(self, n: int) -> list[Sample]:
        xs = []
        for i in range(n):
            xs.append(Sample(i, 1000 + i * 100, 0.5, i > 0))
        return xs


r = Rig()
ss = r.gather(3)
print(len(ss))
for s in ss:
    print(s.id)
    print(s.mv)
    print(s.ok)
ss[1].bump(7)
print(ss[1].mv)
print(ss[2].scaled())
h = ss[0]
h.bump(1)
print(h.mv)
print(ss[0].mv)
print("END")
