# expect: match
# doc: https://docs.pymcu.org/roadmap/#language
class Codes:
    OK = 2
x = 2
match x:
    case Codes.OK:
        print("ok")
    case _:
        print("bad")
print("END")
