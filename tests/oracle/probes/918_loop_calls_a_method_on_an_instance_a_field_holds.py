# expect: match
# doc: https://docs.pymcu.org/roadmap/#language
# A method that changes an instance a field holds (`self.w.read()` advances its phase), called in a
# loop. The loop body is lowered once and runs many times, so what the first turn left in the
# fields must not fold the tests of the second. The invalidation of the fields a call writes only
# knew a receiver that was a bare name; `self.w` is a field holding an instance.
class Wire:
    def __init__(self) -> None:
        self.a = bytearray(2)
        self.b = bytearray(2)
        self.a[0] = 0x12
        self.a[1] = 0x34
        self.b[0] = 0x12
        self.b[1] = 0xB4
        self.alive_a = True
        self.alive_b = True
        self.pos = 0
        self.phase = 0

    def bit_of(self, rom: bytearray, pos: int) -> int:
        return (rom[pos // 8] >> (pos % 8)) & 1

    def reset(self) -> None:
        self.alive_a = True
        self.alive_b = True
        self.pos = 0
        self.phase = 0

    def read(self) -> int:
        v = 1
        if self.phase == 0:
            if self.alive_a and self.bit_of(self.a, self.pos) == 0:
                v = 0
            if self.alive_b and self.bit_of(self.b, self.pos) == 0:
                v = 0
            self.phase = 1
        else:
            if self.alive_a and self.bit_of(self.a, self.pos) == 1:
                v = 0
            if self.alive_b and self.bit_of(self.b, self.pos) == 1:
                v = 0
            self.phase = 2
        return v

    def write(self, bit: int) -> None:
        if self.alive_a and self.bit_of(self.a, self.pos) != bit:
            self.alive_a = False
        if self.alive_b and self.bit_of(self.b, self.pos) != bit:
            self.alive_b = False
        self.pos = self.pos + 1
        self.phase = 0


class Bus:
    def __init__(self) -> None:
        self.w = Wire()

    def run(self, diff: int) -> int:
        self.w.reset()
        i = 16
        for byte in range(2):
            for bit in range(8):
                b = self.w.read()
                if self.w.read():
                    if b:
                        return 99
                elif not b:
                    if diff > i:
                        b = 1
                print(b)
                print(i)
                self.w.write(b)
                i -= 1
        return 0


bus = Bus()
print(bus.run(17))
print("END")
