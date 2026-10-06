# expect: match
# doc: https://docs.pymcu.org/roadmap/#language
x = 3
match x:
    case 1 | 2:
        print("small")
    case 3:
        print("three")
    case _:
        print("other")
print("END")
