# expect: match
# doc: https://docs.pymcu.org/limitations/#functions-and-closures
def double(n):
    return n * 2

class Worker:
    def __init__(self):
        self.cb = double
    def run(self, n):
        return self.cb(n)

w = Worker()
print(w.run(3))
print("END")
