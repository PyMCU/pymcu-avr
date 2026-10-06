# expect: match
# doc: https://docs.pymcu.org/limitations/#classes-and-inheritance
class Counter:
    def __init__(self, n):
        self.n = n
    def __lt__(self, other):
        print("lt")
        return 0
    def __eq__(self, other):
        print("eq")
        return 1
a = Counter(1)
b = Counter(5)
while a < b:
    print(9)
if a == b and a.n == 1:
    print(1)
else:
    print(0)
if a == b or a.n == 2:
    print(1)
else:
    print(0)
print("END")
