# expect: match
# doc: https://docs.pymcu.org/limitations/#compile-time-sequences-in-classes
# A row slice of a 2-D grid is a COPY, like any Python list slice -- never a
# view. Before, `g[y][:]` refused outright with a message that got Python's
# own slice semantics backwards ("a slice would be a view object").
cells = [[0] * 4 for _ in range(3)]
cells[0][1] = 5
cells[2][3] = 7
first_row = cells[0][:]
cells[0][1] = 9
print(first_row[1], cells[0][1])
for y in range(3):
    old_row = cells[y][:]
    cells[y][0] = y + 1
    print(old_row[0], old_row[3], cells[y][0])
print("END")
