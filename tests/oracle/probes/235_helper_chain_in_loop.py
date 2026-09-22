# expect: match
# doc: docs/language/roadmap.md
class Dev:
    def __init__(self):
        for i in range(2):
            self._setup()
    def _setup(self):
        self._reset()
    def _reset(self):
        self.state = 70000

d = Dev()
print(d.state)
print("END")
