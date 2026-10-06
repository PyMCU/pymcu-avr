# expect: match
# doc: https://docs.pymcu.org/roadmap/
x = 0

def f():
    global x
    for i in range(2):
        x = x + 300

f()
print(x)
print("END")
