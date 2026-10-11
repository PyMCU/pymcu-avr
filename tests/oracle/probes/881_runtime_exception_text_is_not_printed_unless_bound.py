# expect: match
# doc: https://docs.pymcu.org/roadmap/#language
# A handler that binds nothing, or binds a different type, still catches; only a bound one
# of the right type reads the text. The behaviour is the same either way.
def quot(a, b):
    return a // b


try:
    quot(1, 0)
except ZeroDivisionError:
    print("caught")
try:
    quot(1, 0)
except ValueError as e:
    print("wrong handler", e)
except ZeroDivisionError:
    print("caught again")
try:
    quot(1, 0)
except Exception as e:
    print(e)
print("END")
