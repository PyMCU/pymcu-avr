# expect: refuse recursive
# doc: https://docs.pymcu.org/limitations/#exception-handling
def f(n):
    if n == 0:
        return 0
    return f(n - 1) + 1
print(f(2))
print("END")
