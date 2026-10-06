# expect: match
# doc: https://docs.pymcu.org/limitations/
# float('inf') / float('nan') / -float('inf') used to refuse ("not a number") -- the
# target already printed inf/nan correctly (_f32_repr reads the IEEE-754 exponent/mantissa
# bit pattern directly), this was a parsing gap, not a representation one.
print(float('inf'))
print(float('nan'))
print(-float('inf'))
print(float('-inf'))
print(float('Infinity'))
print(float('+inf'))
print("END")
