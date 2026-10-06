# expect: match
# doc: https://docs.pymcu.org/roadmap/#language
d = {0: 10, "mid": 2}
print(0 in d)
print(99 in d)
print("mid" in d)
print("END")
