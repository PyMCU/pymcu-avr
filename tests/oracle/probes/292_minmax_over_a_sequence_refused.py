# expect: refuse does not consult the class
# doc: docs/language/limitations.md:412
class Cell:
    def __init__(self, n):
        self.n = n
    def __lt__(self, other):
        return self.n < other.n
a = Cell(3)
b = Cell(1)
xs = [a, b]
m = max(xs)
print(m.n)
print("END")
