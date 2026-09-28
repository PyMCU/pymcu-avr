# expect: match
# doc: docs/language/limitations.md:1048
# sum() over a generator expression folds by compile-time unroll like the fixed-array form
# (probes 460 and 523): each element is added once, in order. It used to be refused as an
# unsupported generator expression (#432).
print(sum(x for x in [1, 2, 3]))
print("END")
