# expect: match
# doc: https://docs.pymcu.org/limitations/
# A user object named `sys` wins over the introspection spelling: the program's
# own binding answers the member read. The member is an int because a
# class-level string attribute folded in print position streams the interned
# id, not the text -- a pre-existing emission gap unrelated to binding.
class Fake:
    platform = 7

sys = Fake()
print(sys.platform)
print("END")
