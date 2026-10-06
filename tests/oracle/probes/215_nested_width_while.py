# expect: match
# doc: https://docs.pymcu.org/roadmap/
class Acc:
    def __init__(self):
        self.total = 0
    def run(self):
        i = 0
        while i < 4:
            self.total = self.total + 300
            i = i + 1

a = Acc()
a.run()
print(a.total)
print("END")
