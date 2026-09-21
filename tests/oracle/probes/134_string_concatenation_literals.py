# expect: match
# doc: docs/language/limitations.md:777
"""Fixed at #438: `a + b` of two string variables folds their texts.

It used to add the two interned ids as integers, so this printed another
string's text or a bare id where "helloworld" was meant.
"""
a = "hello"
b = "world"
s = a + b
print(s)
print("END")
