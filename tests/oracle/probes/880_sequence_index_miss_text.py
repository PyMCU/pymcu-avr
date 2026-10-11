# expect: match
# doc: https://docs.pymcu.org/roadmap/#language
# list.index and tuple.index word a miss differently, and the caught exception says which.
xs = [1, 2, 3]
ts = (1, 2, 3)
k = 99
try:
    print(xs.index(k))
except ValueError as e:
    print(e)
try:
    print(ts.index(k))
except ValueError as e:
    print(e)
print("END")
