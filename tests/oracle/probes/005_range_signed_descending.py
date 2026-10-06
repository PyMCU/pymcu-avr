# expect: match
# doc: https://docs.pymcu.org/roadmap/#language
s = 0
for i in range(5, -4, -3):
    print(i)
    s = s + i
print(s)
print("END")
