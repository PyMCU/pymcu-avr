# expect: match
# doc: docs/language/roadmap.md:70
d = {0: 10, "mid": 2}
print(0 in d)
print(99 in d)
print("mid" in d)
print("END")
