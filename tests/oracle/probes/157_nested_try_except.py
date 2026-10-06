# expect: match
# doc: https://docs.pymcu.org/roadmap/#language
def risky(x):
    if x == 1:
        raise ValueError("inner")
    return x
def run(x):
    try:
        try:
            return risky(x)
        except TypeError:
            return -1
    except ValueError:
        return -2
print(run(1))
print(run(5))
print("END")
