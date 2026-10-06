# expect: match
# doc: https://docs.pymcu.org/roadmap/#language
x = 9
match x:
    case value:
        print(value + 1)
print("END")
