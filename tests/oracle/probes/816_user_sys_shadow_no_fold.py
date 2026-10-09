# expect: match
# doc: https://docs.pymcu.org/limitations/
# A user object named `sys` wins over the introspection spelling: the program's
# own binding answers the member read.
class Fake:
    platform = "mine"

sys = Fake()
print(sys.platform)
print("END")
