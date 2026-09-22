# expect: match
# doc: docs/language/roadmap.md
class Dev:
    def __init__(self):
        self.acc = 0
        for i in range(2):
            self._bump()
    def _bump(self):
        self.acc = self.acc + 300

d = Dev()
print(d.acc)
print("END")
