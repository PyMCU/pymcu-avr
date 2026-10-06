# expect: refuse has no runtime __str__
# doc: https://docs.pymcu.org/limitations/#async-and-concurrency
class Point:
    def __init__(self, x, y):
        self.x = x
        self.y = y
    def __str__(self):
        return "Point"
p = Point(1, 2)
print(p)
print("END")
