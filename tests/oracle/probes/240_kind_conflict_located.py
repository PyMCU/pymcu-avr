# expect: refuse main.py:7:14: error: CompileError: field 'x' is first typed as numeric and is later given a str value
# doc: https://docs.pymcu.org/limitations/
class Rec:
    def __init__(self):
        self.x = 5
    def m(self):
        self.x = "nested"

r = Rec()
r.m()
print("END")
