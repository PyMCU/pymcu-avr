# expect: match
# doc: https://docs.pymcu.org/limitations/#compile-time-sequences-in-classes
# A negative step's omitted-bound defaults are the LAST index and "before
# index 0", not the positive-step defaults 0 and w. Reusing the
# positive-step defaults regardless of sign made `cells[0][::-1]` copy zero
# elements: start=0, stop=w, and a negative step fails `i > stop`
# immediately.
cells = [[0] * 3 for _ in range(1)]
cells[0][0] = 5
cells[0][1] = 6
cells[0][2] = 7
row = cells[0][::-1]
print(len(row))
print(row[0], row[1], row[2])
print("END")
