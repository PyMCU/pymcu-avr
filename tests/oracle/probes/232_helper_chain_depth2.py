# expect: match
# doc: docs/language/roadmap.md
class Dev:
    def __init__(self):
        self._setup()
    def _setup(self):
        self._reset()
    def _reset(self):
        self.state = 305419896

d = Dev()
print(d.state)
print("END")
