# expect: match
# doc: https://docs.pymcu.org/roadmap/#language
# A module constant sizes a buffer in a method that a loop calls (`bytearray(NB)`, NB = 8). The size
# was found only through a fact the loop dropped, and a literal 8 compiled.
NB = 8


class Bus:
    def search(self, diff: int) -> int:
        rom = bytearray(NB)
        rom[0] = diff
        return rom[0]

    def scan(self) -> int:
        diff = 3
        for _ in range(3):
            diff = self.search(diff)
        return diff


b = Bus()
print(b.scan())
print("END")
