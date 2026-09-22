# expect: match
# doc: docs/language/roadmap.md
class Cfg:
    def __init__(self):
        self.val = 0
    def apply(self):
        try:
            self.val = 700
        except ValueError:
            self.val = 1

c = Cfg()
c.apply()
print(c.val)
print("END")
