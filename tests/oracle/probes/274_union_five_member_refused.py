# expect: refuse tagged unions carry at most 4
# doc: https://docs.pymcu.org/limitations/
# RFC 0009 section 6/6.1: the tag byte holds a member index and the reader
# count grows with the member count -- five is past the design ceiling.
from typing import Union


def too_many(k: int) -> Union[int, float, str, bool, None]:
    return k


print(too_many(1))
print("END")
