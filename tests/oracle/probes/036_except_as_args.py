# expect: match
# doc: https://docs.pymcu.org/roadmap/#language
def risky():
    raise ValueError("bad")
try:
    risky()
except ValueError as e:
    print(e.args[0])
print("END")
