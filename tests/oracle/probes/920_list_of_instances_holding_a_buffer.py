# expect: match
# doc: https://docs.pymcu.org/roadmap/#language
# A growable list of class instances that each hold a buffer (adafruit_onewire collects
# `devices.append(OneWireAddress(rom))`, one record per sensor found). Every element is its own
# copy of the bytes it was appended with, whatever the name the buffer had in the loop, and the
# record is read back by index, by name, by property and by iteration.
class Addr:
    def __init__(self, rom: bytearray) -> None:
        self._rom = rom

    @property
    def rom(self) -> bytearray:
        return self._rom


class Finder:
    def scan(self, n: int) -> list[Addr]:
        devs = []
        for k in range(n):
            rom = bytearray(8)
            for i in range(8):
                rom[i] = k * 16 + i
            devs.append(Addr(rom))
        return devs


f = Finder()
found = f.scan(3)
print(len(found))
print(found[0].rom[3])
print(found[1].rom[3])
print(found[2].rom[7])
r = found[1].rom
print(r[0], r[7])
total = 0
for d in found:
    total = total + d.rom[1]
print(total)
print("END")
