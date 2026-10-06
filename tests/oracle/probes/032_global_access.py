# expect: match
# doc: https://docs.pymcu.org/roadmap/#language
count = 1
def inc():
    global count
    count = count + 2
inc()
print(count)
print("END")
