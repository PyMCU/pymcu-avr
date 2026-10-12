# expect: match
# doc: https://docs.pymcu.org/roadmap/#language
# adafruit_irremote's GenericTransmit keeps the lists its constructor was handed in fields
# and walks them with `for i, _ in enumerate(self.header)`: enumerate over a field list
# that was never written as a literal in the class.
class Frame:
    def __init__(self, header: list[int], one: list[int]):
        self.header = header
        self.one = one

    def weigh(self) -> int:
        total = 0
        for i, _ in enumerate(self.header):
            total += self.header[i] * (i + 1)
        return total

    def spread(self) -> int:
        total = 0
        for i, v in enumerate(self.one):
            total += v + i
        return total


f = Frame([9, 4], [5, 7, 3])
print(f.weigh())
print(f.spread())
print("END")
