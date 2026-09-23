# expect: match
# doc: docs/language/roadmap.md:49
# `needle in s` on a compile-time string name folds to substring membership;
# the refusal this probe was written for predates that feature (cbd581d4).
s = "hello"
print('e' in s)
print("END")
