# expect: match
# doc: docs/language/roadmap.md
class Life:
    def __init__(self, seed):
        self.rng = seed
    def step(self):
        for x in range(4):
            self.rng = (self.rng * 1103515245 + 12345) & 0x7FFFFFFF

l = Life(1)
l.step()
print(l.rng)
print("END")
