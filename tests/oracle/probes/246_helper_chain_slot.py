# expect: match
# doc: https://docs.pymcu.org/roadmap/
from pymcu.types import outline

class Dev:
    def __init__(self):
        self.id = 1
        self.rate = 2
        self._setup()
    def _setup(self):
        self._reset()
    def _reset(self):
        self.state = 305419896
    @outline
    def read(self) -> int:
        return self.id + self.rate

d = Dev()
print(d.read())
print(d.state)
print("END")
