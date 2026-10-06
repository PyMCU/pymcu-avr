# expect: match
# doc: https://docs.pymcu.org/roadmap/#language
def check(n: int) -> int:
    if n > 0:
        pass
    else:
        return -1
    return n
print(check(5))
print(check(-5))
print("END")
