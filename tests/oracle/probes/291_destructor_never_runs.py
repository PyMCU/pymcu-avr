# expect: divergence https://docs.pymcu.org/limitations/#classes-and-inheritance (destructors never run)
# doc: https://docs.pymcu.org/limitations/#classes-and-inheritance
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
