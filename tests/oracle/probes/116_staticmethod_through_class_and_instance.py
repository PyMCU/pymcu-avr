# expect: match
# doc: https://docs.pymcu.org/roadmap/#language
class Util:
    @staticmethod
    def double(n):
        return n * 2

print(Util.double(3))
u = Util()
print(u.double(3))
print("END")
