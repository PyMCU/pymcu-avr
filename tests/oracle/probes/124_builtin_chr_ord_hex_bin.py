# expect: match
# doc: https://docs.pymcu.org/limitations/#async-and-concurrency
def make(n: int) -> int:
    return n
print(chr(make(66)))
print(ord('A'))
print("END")
