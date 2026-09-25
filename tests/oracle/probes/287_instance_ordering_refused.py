# expect: refuse defines no __lt__
# doc: docs/language/limitations.md:412
class Plain:
    def __init__(self, n):
        self.n = n
a = Plain(3)
b = Plain(4)
if a < b:
    print(1)
print("END")
