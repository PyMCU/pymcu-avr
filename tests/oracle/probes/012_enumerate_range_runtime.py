# expect: match
# doc: https://docs.pymcu.org/roadmap/#language
def walk(n):
    for i, x in enumerate(range(2, n)):
        print(i * 100 + x)
walk(5)
print("END")
