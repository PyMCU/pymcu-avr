# expect: match
# doc: https://docs.pymcu.org/roadmap/#language
# A name initialised to 0 and later given a float: `h = 0` then `h = 55.5`, and an accumulator that
# starts at 0. The name was typed an integer by its first write and the float was truncated to its
# integer part (55.0 for 55.5; 3.0 for 3.75).
def f(n: int) -> float:
    h = 0
    if n > 1:
        h = 55.5
    return h


def total(a: float, b: float) -> float:
    t = 0
    t = t + a
    t = t + b
    return t


print(f(3))
print(total(1.5, 2.25))
print("END")
