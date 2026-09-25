# expect: match
# doc: docs/language/limitations.md:392
class Acc:
    def __init__(self, n):
        self.n = n
    def __add__(self, other):
        return self.n + other.n + 11
    def __eq__(self, other):
        print("eq")
        return 0
a = Acc(1)
b = Acc(2)
print(a + b)
if a == b:
    print(1)
else:
    print(0)
print("END")
