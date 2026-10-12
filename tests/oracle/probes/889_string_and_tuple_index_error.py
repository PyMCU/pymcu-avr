# expect: match
# doc: https://docs.pymcu.org/roadmap/#language
# A run-time index past the end of a string or a tuple of numbers raises IndexError with the
# type's own wording.
s = "abc"
t = (5, 6, 7)


def pick(n: int) -> int:
    return n


k = pick(3)
try:
    print(s[k])
except IndexError as e:
    print(e)
try:
    print(t[k])
except IndexError as e:
    print(e)
print(s[pick(1)])
print(t[pick(1)])
print("END")
