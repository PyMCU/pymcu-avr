# expect: match
# doc: https://docs.pymcu.org/roadmap/#language
def total(n):
    s = 0
    for i in range(n):
        s = s + i
    return s
print(total(6))
print(total(0))
print("END")
