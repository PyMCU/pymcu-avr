# expect: match
# doc: https://docs.pymcu.org/roadmap/#language
import pymcu.types as t
x: t.uint8 = 200
y: t.uint8 = 100
print(x + y)
print("END")
