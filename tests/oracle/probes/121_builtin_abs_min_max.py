# expect: match
# doc: https://docs.pymcu.org/limitations/#async-and-concurrency
def clamp(n: int) -> int:
    return abs(n)
print(clamp(-7))
print(min(3, 7))
print(max(3, 7))
print("END")
