# expect: match
# doc: https://docs.pymcu.org/roadmap/#language
from pymcu.types import uint8

grid: list[list[uint8]] = [[1, 2], [3, 4]]
row: list[uint8] = grid[1]
print(row[0], row[1])
row[0] = 9
print(grid[1][0])
print("END")
