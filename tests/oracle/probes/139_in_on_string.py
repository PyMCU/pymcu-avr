# expect: match
# doc: https://docs.pymcu.org/roadmap/#language
# `needle in s` on a compile-time string name folds to substring membership;
# the refusal this probe was written for predates that feature (cbd581d4).
s = "hello"
print('e' in s)
print("END")
