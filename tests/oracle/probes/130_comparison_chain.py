# expect: match
# doc: https://docs.pymcu.org/language-reference/#primitive-types
def check(a: int, b: int, c: int) -> bool:
    return a < b < c
print(check(1, 2, 3))
print(check(1, 5, 3))
print("END")
