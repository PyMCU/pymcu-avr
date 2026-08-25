# PyMCU -- str-class-field: a string held in a field of a class.
#
# The remaining half of PyMCU#80. Printing a str local or a str parameter was fixed there;
# printing a str FIELD was not, and it sent 256, the string's interned id, because the read
# fell through to the numeric writer. Two instances printed 256 and 257, consecutive ids,
# which is what made the number recognisable for what it was.
#
# Both spellings are here: through a method (`print(self.n)`) and from outside
# (`print(o.n)`), and two instances, since sharing one id would print the same text twice.
#
# Expected UART output:
#   hi
#   hi
#   bye
#   done
from pymcu.hal.console import print


class Label:
    def __init__(self, n: str):
        self.n: str = n

    def show(self):
        print(self.n)


def main():
    o = Label("hi")
    o.show()
    print(o.n)

    p = Label("bye")
    p.show()

    print("done")
