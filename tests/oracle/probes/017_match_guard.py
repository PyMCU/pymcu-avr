# expect: match
# doc: https://docs.pymcu.org/roadmap/#language
x = 12
match x:
    case n if n > 10:
        print(n)
    case _:
        print(0)
print("END")
