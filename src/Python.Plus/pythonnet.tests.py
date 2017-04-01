import pythonnet
import sys
tst = pythonnet.ThreadedDemuxerTextIO(None)
tst.cur_thread_io = sys.stdout

sys.stdout = tst

print("Hello from multithreaded world")