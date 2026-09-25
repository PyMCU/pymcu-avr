# expect: refuse generator expressions are not supported
# doc: docs/language/limitations.md:520
# tracked: #432
print(sum(x for x in [1, 2, 3]))
print("END")
