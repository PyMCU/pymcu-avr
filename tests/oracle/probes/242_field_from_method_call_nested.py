# expect: match
# doc: docs/language/roadmap.md
# tracked: PyMCU/PyMCU#489
class Dev:
    def __init__(self):
        for i in range(1):
            self.v = self._read()
    def _read(self):
        return 300

d = Dev()
print(d.v)
print("END")
