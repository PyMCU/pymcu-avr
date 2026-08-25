state: str = "idle"


def bump():
    global state
    state = "running"
