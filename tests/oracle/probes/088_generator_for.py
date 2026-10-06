# expect: match
# doc: https://docs.pymcu.org/roadmap/#mcu-extensions
def gen(n):
    for i in range(n):
        yield i * 2
for x in gen(4):
    print(x)
print("END")
