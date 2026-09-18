# namedtuple: compile-time class factory for adafruit_irremote's IRMessage.
#
# WHAT DISCRIMINATES:
#   3  -- p.x from Point(3, 5)
#   5  -- p.y
#   1  -- isinstance(p, Point)
#   7  -- keyword construction Point(x=7, y=9)
from collections import namedtuple
from pymcu.time import delay_ms

Point = namedtuple("Point", ("x", "y"))


def main():
    while True:
        p = Point(3, 5)
        q = Point(x=7, y=9)
        print(p.x)
        print(p.y)
        print(1 if isinstance(p, Point) else 0)
        print(q.x)
        print("END")
        delay_ms(1200)
