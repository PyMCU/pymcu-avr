# expect: match
# doc: docs/language/limitations.md:407
class Plain:
    def __init__(self, n):
        self.n = n
a = Plain(3)
b = Plain(3)
c = a
if a == b:
    print(1)
else:
    print(0)
if a != b:
    print(1)
else:
    print(0)
if a is b:
    print(1)
else:
    print(0)
if a is c:
    print(1)
else:
    print(0)
if a is not b:
    print(1)
else:
    print(0)
print("END")
