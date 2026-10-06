# expect: match
# doc: https://docs.pymcu.org/roadmap/#language
def risky():
    raise ValueError("boom")
def wrapper():
    try:
        risky()
    except ValueError:
        print("caught, reraising")
        raise
try:
    wrapper()
except ValueError:
    print("outer caught")
print("END")
