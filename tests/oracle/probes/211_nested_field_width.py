# expect: match
# doc: docs/language/roadmap.md
# tracked: PyMCU/PyMCU#488
class Life:
    def __init__(self, seed):
        self.rng = seed
    def step(self):
        for y in range(2):
            for x in range(2):
                self.rng = (self.rng * 1103515245 + 12345) & 0x7FFFFFFF

l = Life(1)
l.step()
print(l.rng)
print("END")
