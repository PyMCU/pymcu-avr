# expect: match
# doc: docs/language/roadmap.md
class Dev:
    def __init__(self):
        self._a()
    def _a(self):
        self._b()
    def _b(self):
        self._c()
    def _c(self):
        self.state = 305419896

d = Dev()
print(d.state)
print("END")
