# expect: match
# doc: https://docs.pymcu.org/roadmap/#language
# A list field declared with its length is a fixed array whose elements change at run time.
class Bag:
    def __init__(self):
        self.items: list[int] = [0, 0, 0]

    def set(self, i: int, x: int):
        self.items[i] = x

    def walk(self):
        for i, v in enumerate(self.items):
            print(i * 10 + v)

    def walk_from(self, first: int):
        for i, v in enumerate(self.items, first):
            print(i * 10 + v)


b = Bag()
b.set(0, 3)
b.set(2, 5)
b.walk()
b.walk_from(2)
print("END")
