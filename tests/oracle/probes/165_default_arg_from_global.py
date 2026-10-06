# expect: match
# doc: https://docs.pymcu.org/roadmap/#language
GAIN = 4
def scale(x, mul=GAIN):
    return x * mul
print(scale(3))
print("END")
