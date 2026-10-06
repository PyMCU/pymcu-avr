# expect: match
# doc: https://docs.pymcu.org/roadmap/#language
i = 0
while i < 3:
    print(i)
    i = i + 1
else:
    print(9)
print("END")
