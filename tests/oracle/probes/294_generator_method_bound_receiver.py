# expect: match
# doc: https://docs.pymcu.org/roadmap/#language
class Steps:
    def __init__(self, seed):
        self.total = seed
    def walk(self, n):
        i = 0
        while i < n:
            self.total = self.total + i
            yield self.total
            i = i + 1
s = Steps(1)
for v in s.walk(4):
    print(v)
print(s.total)
for w in s.walk(9):
    if w > 20:
        break
    print(w)
print(s.total)
for z in s.walk(2):
    print(z)
print(s.total)
print("END")
