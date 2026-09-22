# expect: match
# doc: docs/language/roadmap.md
from pymcu.types import uint16

class Dev:
    def __init__(self):
        self.v = self._read()
    def _read(self) -> uint16:
        return 300

d = Dev()
print(d.v)
print("END")
