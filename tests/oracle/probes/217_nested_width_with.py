# expect: match
# doc: docs/language/roadmap.md
class Gate:
    def __init__(self):
        self.open = 0
    def __enter__(self):
        return self
    def __exit__(self, typ, val, tb):
        return False

class Cfg:
    def __init__(self):
        self.val = 0
    def apply(self):
        with Gate():
            self.val = 700

c = Cfg()
c.apply()
print(c.val)
print("END")
