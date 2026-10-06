# expect: refuse f-string
# doc: https://docs.pymcu.org/limitations/#dynamic-memory-and-containers
def use(s):
    print(s)
x = 7
use(f"x={x}")
print("END")
