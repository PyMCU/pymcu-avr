# expect: match
# doc: https://docs.pymcu.org/roadmap/#language
def quot(a, b):
    return a // b
try:
    print(quot(8, 0))
except ZeroDivisionError:
    print(100)
print("END")
