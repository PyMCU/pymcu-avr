# expect: match
# doc: https://docs.pymcu.org/roadmap/#language
pair = [4, 5]
match pair:
    case [4, y]:
        print(y)
    case _:
        print(0)
print("END")
