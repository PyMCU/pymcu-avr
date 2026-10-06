# expect: refuse cannot tell which class
# doc: https://docs.pymcu.org/roadmap/#language
class Gen:
    def values(self):
        yield 1
for x in Gen().values():
    print(x)
print("END")
