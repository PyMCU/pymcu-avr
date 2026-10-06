# expect: match
# doc: https://docs.pymcu.org/roadmap/#language
for a, b in zip([1, 2, 3], [9, 8, 7]):
    print(a * 10 + b)
print("END")
