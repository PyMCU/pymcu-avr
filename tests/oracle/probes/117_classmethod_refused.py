# expect: match
# doc: https://docs.pymcu.org/roadmap/#mcu-extensions
class Util:
    @classmethod
    def make(cls):
        return cls()
    def __init__(self):
        self.value = 5

m = Util.make()
print(m.value)
print("END")
