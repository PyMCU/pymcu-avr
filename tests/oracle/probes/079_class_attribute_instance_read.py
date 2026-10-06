# expect: match
# doc: https://docs.pymcu.org/roadmap/#language
class Device:
    SCALE = 5
    def read(self):
        return self.SCALE + 1
d = Device()
print(d.SCALE)
print(d.read())
print("END")
