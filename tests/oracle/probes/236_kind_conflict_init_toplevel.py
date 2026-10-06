# expect: refuse is first typed as numeric and is later given a str value
# doc: https://docs.pymcu.org/limitations/
class Rec:
    def __init__(self):
        self.x = 5
        self.x = "nested"
    def show(self):
        print(self.x)

r = Rec()
r.show()
print("END")
