# expect: divergence docs/language/limitations.md:388
# doc: docs/language/limitations.md:388
class Held:
    def __init__(self, n):
        self.n = n
    def __del__(self):
        print("DEL")
def use():
    h = Held(4)
    print(h.n)
use()
print(0)
print("END")
