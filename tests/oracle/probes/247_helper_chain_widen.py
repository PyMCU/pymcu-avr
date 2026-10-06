# expect: match
# doc: https://docs.pymcu.org/roadmap/
class Dev:
    def __init__(self):
        self.state = 0
        self._setup()
    def _setup(self):
        self._reset()
    def _reset(self):
        self.state = 305419896

d = Dev()
print(d.state)
print("END")
