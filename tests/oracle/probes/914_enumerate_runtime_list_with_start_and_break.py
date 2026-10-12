# expect: match
# doc: https://docs.pymcu.org/roadmap/#language
data: list[int] = []
for k in range(5):
    data.append(k + 1)
for i, v in enumerate(data, 4):
    if v == 4:
        break
    print(i * 10 + v)
print(i)
print("END")
