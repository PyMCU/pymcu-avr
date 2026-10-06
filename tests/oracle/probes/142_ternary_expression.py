# expect: match
# doc: https://docs.pymcu.org/roadmap/#language
def sign(n: int) -> int:
    return 1 if n >= 0 else -1
print(sign(5))
print(sign(-5))
print("END")
