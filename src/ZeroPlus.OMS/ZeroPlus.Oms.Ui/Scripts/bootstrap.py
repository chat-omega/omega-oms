import sys

class PyNetConsole(object):
    def __init__(self, writeCallback):
        self.writeCallback = writeCallback

    def write(self, message):
        self.writeCallback(message)

    def flush(self):
        pass

def set_console_out(writeCallback):
    sys.stdout = PyNetConsole(writeCallback)