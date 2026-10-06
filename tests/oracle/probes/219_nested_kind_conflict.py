# expect: refuse is first typed as numeric and is later given a str value
# doc: https://docs.pymcu.org/roadmap/
class Rec:
    def __init__(self):
        self.x = 5
    def m(self):
        for i in range(1):
            self.x = "nested"
    def show(self):
        print(self.x)

r = Rec()
r.m()
r.show()
print("END")
