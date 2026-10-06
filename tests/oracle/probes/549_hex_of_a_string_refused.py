# expect: refuse hex() argument must be an integer
# doc: https://docs.pymcu.org/limitations/
# hex()/bin()/oct() of a STRING constant used to silently hex-encode the interned string id
# instead of raising: the same discriminator print() uses (a Constant carrying Text stands
# for text, not a number) now refuses it outright. CPython raises TypeError ("'str' object
# cannot be interpreted as an integer").
print(hex("A"))
print("END")
