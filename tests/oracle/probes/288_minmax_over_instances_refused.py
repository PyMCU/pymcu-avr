# expect: refuse does not consult the class
# doc: docs/language/limitations.md:407
class Cell:
    def __init__(self, n):
        self.n = n
    def __lt__(self, other):
        return self.n < other.n
a = Cell(3)
b = Cell(1)
m = max(a, b)
print(m.n)
print("END")
