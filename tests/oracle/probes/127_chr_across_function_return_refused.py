# expect: match
# doc: docs/language/limitations.md:785
def make(n: int) -> int:
    return chr(n)
print(make(66))
print("END")
