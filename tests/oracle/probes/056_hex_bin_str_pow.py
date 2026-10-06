# expect: match
# doc: https://docs.pymcu.org/roadmap/#language
"""Fixed at #393: hex() and bin() of a constant print their text.

They used to print the interned id of the folded string as a decimal number,
257 and 258, while str() on the same line came out right -- str() is one of the
shapes print recognised by syntax. pow() and ** fold to numbers and are the
control: they must keep printing numbers.
"""
print(hex(255))
print(bin(10))
print(str(42))
print(pow(2, 5))
print(3 ** 3)
print("END")
