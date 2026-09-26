# expect: refuse cannot tell which class
# doc: docs/language/roadmap.md:115
class Gen:
    def values(self):
        yield 1
for x in Gen().values():
    print(x)
print("END")
