# expect: match
# doc: docs/language/limitations.md:769
def clamp(n: int) -> int:
    return abs(n)
print(clamp(-7))
print(min(3, 7))
print(max(3, 7))
print("END")
