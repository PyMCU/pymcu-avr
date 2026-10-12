# expect: match
# doc: https://docs.pymcu.org/limitations/#iterators-and-comprehensions
# A class of several scalar fields appended to a growable list (RFC 0016, the general record): each
# element is a heap copy of the fields, read back by index, by name and by iteration, and written
# through the name or the index.
class Dev:
    def __init__(self, addr: int, kind: int, cal: int) -> None:
        self.addr = addr
        self.kind = kind
        self.cal = cal


class Finder:
    def scan(self, n: int) -> list[Dev]:
        found = []
        for k in range(n):
            found.append(Dev(k + 10, k * 2, 300 + k))
        return found


f = Finder()
devs = f.scan(3)
print(len(devs))
d = devs[1]
print(d.addr)
print(d.kind)
print(d.cal)
d.kind = 99
print(devs[1].kind)
print(devs[2].cal)
t = 0
for x in devs:
    t = t + x.addr + x.cal
print(t)
print("END")
