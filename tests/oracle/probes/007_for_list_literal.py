# expect: match
# doc: https://docs.pymcu.org/roadmap/#language
s = 0
for x in [3, 1, 4, 1]:
    s = s * 10 + x
print(s)
print("END")
