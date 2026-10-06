# expect: match
# doc: https://docs.pymcu.org/roadmap/#language
def show(**kwargs):
    return kwargs['a'] + kwargs['b']
print(show(a=1, b=2))
print("END")
