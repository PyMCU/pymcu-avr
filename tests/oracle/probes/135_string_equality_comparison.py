# expect: match
# doc: docs/language/limitations.md:778
# tracked: #438
x = "abc"
if x == "abc":
    print("yes")
else:
    print("no")
print("END")
