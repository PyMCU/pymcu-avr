# expect: match
# doc: https://docs.pymcu.org/roadmap/#language
# KeyError's text is the repr of the missing key: quoted for a string, bare for a number.
d = {"a": 1, "b": 2}
k = "zz"
try:
    print(d[k])
except KeyError as e:
    print(e)
try:
    print(d["nope"])
except KeyError as e:
    print(e)
n = {1: 10, 2: 20}
m = 9
try:
    print(n[m])
except KeyError as e:
    print(e)
print("END")
