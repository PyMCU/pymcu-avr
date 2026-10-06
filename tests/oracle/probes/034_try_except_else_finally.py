# expect: match
# doc: https://docs.pymcu.org/roadmap/#language
def risky(x):
    if x > 2:
        raise ValueError("large")
    return x + 10
for x in [1, 3]:
    try:
        print(risky(x))
    except ValueError:
        print(99)
    else:
        print(55)
    finally:
        print(77)
print("END")
