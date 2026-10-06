# expect: match
# doc: https://docs.pymcu.org/roadmap/#language
first, *rest, last = (1, 2, 3, 4)
print(first)
print(rest[0])
print(rest[1])
print(last)
print("END")
