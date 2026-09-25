# expect: match
# doc: docs/language/limitations.md:392
class Cell:
    def __init__(self, n):
        self.n = n
    def __eq__(self, other):
        print("eq")
        return 0
    def __add__(self, other):
        return self.n + other.n + 7
class Owner:
    def __init__(self):
        self.lhs = Cell(1)
        self.rhs = Cell(2)
o = Owner()
if o.lhs == o.rhs:
    print(1)
else:
    print(0)
print(o.lhs + o.rhs)
print("END")
