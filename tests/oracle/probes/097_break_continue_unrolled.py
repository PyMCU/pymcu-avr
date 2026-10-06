# expect: match
# doc: https://docs.pymcu.org/roadmap/#language
for i in range(5):
    if i == 1:
        continue
    if i == 4:
        break
    print(i)
print("END")
