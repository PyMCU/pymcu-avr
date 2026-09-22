# expect: match
# doc: docs/language/roadmap.md
x = 0

def f():
    global x
    for i in range(2):
        x = x + 300

f()
print(x)
print("END")
