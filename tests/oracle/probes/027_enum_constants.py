# expect: match
# doc: https://docs.pymcu.org/roadmap/#language
from enum import Enum
class Mode(Enum):
    OFF = 0
    ON = 1
print(Mode.ON.value)
print("END")
