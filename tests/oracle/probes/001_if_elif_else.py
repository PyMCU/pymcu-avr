# expect: match
# doc: https://docs.pymcu.org/roadmap/#language
x = 7
if x < 3:
    print("low")
elif x < 10:
    print("mid")
else:
    print("high")
print("END")
