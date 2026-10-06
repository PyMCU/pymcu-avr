# expect: match
# doc: https://docs.pymcu.org/limitations/#classes-and-inheritance
class Cell:
    def __init__(self, n):
        self.n = n
    def __eq__(self, other):
        print("eq")
        return 0
    def __ne__(self, other):
        print("ne")
        return 1
    def __lt__(self, other):
        print("lt")
        return 1
    def __le__(self, other):
        print("le")
        return 0
    def __gt__(self, other):
        print("gt")
        return 1
    def __ge__(self, other):
        print("ge")
        return 0
a = Cell(1)
b = Cell(2)
if a == b:
    print(1)
else:
    print(0)
if a != b:
    print(1)
else:
    print(0)
if a < b:
    print(1)
else:
    print(0)
if a <= b:
    print(1)
else:
    print(0)
if a > b:
    print(1)
else:
    print(0)
if a >= b:
    print(1)
else:
    print(0)
print("END")
