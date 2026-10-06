# expect: match
# doc: https://docs.pymcu.org/roadmap/#language
class Outer:
    class Inner:
        A = 7
print(Outer.Inner.A)
print("END")
