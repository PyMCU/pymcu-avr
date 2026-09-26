# expect: refuse has no value to assign
# doc: LANGUAGE_ROADMAP.md:453
def gen():
    x = (yield 1)
    yield x
for x in gen():
    print(x)
print("END")
