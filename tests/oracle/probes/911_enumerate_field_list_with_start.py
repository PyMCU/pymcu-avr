# expect: match
# doc: https://docs.pymcu.org/roadmap/#language
class Frame:
    def __init__(self, header: list[int]):
        self.header = header

    def weigh(self) -> int:
        total = 0
        for i, v in enumerate(self.header, 3):
            total += v * i
        return total

    def weigh_kw(self) -> int:
        total = 0
        for i, v in enumerate(self.header, start=10):
            total += v + i
        return total


f = Frame([9, 4, 6])
print(f.weigh())
print(f.weigh_kw())
print("END")
