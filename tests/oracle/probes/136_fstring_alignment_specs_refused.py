# expect: refuse unsupported f-string format spec
# doc: https://docs.pymcu.org/roadmap/#language
x = 7
print(f"[{x:>5}]")
print("END")
