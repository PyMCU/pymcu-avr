# probes-life-lists/c5: `g[y] = <new row>` rebinds a row -- the row is a view
# into the flat array, not a variable. EXPECTED BUILD FAILURE.
g = [[0] * 4 for _ in range(2)]
g[1] = [0] * 4
print(g[1][0])
