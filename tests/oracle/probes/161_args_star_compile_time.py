# expect: match
# doc: https://docs.pymcu.org/roadmap/#language
def total(*args):
    s = 0
    for a in args:
        s = s + a
    return s
print(total(1, 2, 3))
print("END")
