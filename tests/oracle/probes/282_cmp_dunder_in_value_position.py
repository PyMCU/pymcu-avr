# expect: match
# doc: docs/language/limitations.md:397
class Cell:
    def __init__(self, n):
        self.n = n
    def __eq__(self, other):
        return self.n + other.n
    def __lt__(self, other):
        return self.n + 20
def go():
    a = Cell(3)
    b = Cell(4)
    v = a == b
    w = a < b
    print(v)
    print(w)
go()
print("END")
