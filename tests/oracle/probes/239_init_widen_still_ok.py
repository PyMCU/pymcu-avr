# expect: match
# doc: docs/language/limitations.md
class Rec:
    def __init__(self):
        self.x = 5
        self.x = 70000
    def show(self):
        print(self.x)

r = Rec()
r.show()
print("END")
