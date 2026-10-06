# expect: match
# doc: https://docs.pymcu.org/roadmap/#language
"""Fixed at #399: a character indexed out of a runtime string prints as itself.

s[2] used to print 48, the code of '0'. Python has no char type: s[2] is the
one-character string at that position, and on this target that string IS the
byte, so the raw byte writer is the answer.
"""
x = 12
s = f"t={x:04d}"
print(s)
print(len(s))
print(s[2])
print("END")
