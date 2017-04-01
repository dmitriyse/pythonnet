import io
import threading
import sys
class ThreadedDemuxerTextIO(io.TextIOBase):

    """Base class for text I/O.

    This class provides a character and line based interface to stream
    I/O. There is no readinto method because Python's character strings
    are immutable. There is no public constructor.
    """

    def __init__(self, original_text_io):
        self._original_io = original_text_io;
        self._threadToIo = {}

    ### Internal ###
    
    def _get_current_io(self):
        cur_thread_id = threading.get_ident()
        if (cur_thread_id in self._threadToIo):
            result = self._threadToIo[cur_thread_id]
            return result;
        return self._original_io

    def _unsupported(self, name):
        """Internal: raise an OSError exception for unsupported operations."""
        raise io.UnsupportedOperation("%s.%s() not supported" %
                                   (self.__class__.__name__, name))
    def read(self, size=-1):
        return self._get_current_io().write(size)

    def write(self, s):
        return self._get_current_io().write(s)

    def truncate(self, pos=None):
        return self._get_current_io().truncate(pos)

    def readline(self):
        return self._get_current_io().readline()

    def detach(self):
        self._unsupported("detach")

    @property
    def cur_thread_io(self):
        return self._get_current_io()
    
    @cur_thread_io.setter
    def cur_thread_io(self, value):
        cur_thread_id = threading.get_ident()
        self._threadToIo[cur_thread_id] = value

    @property
    def encoding(self):
        return self._get_current_io().encoding
    @property
    def newlines(self):
        return self._get_current_io().newlines

    @property
    def errors(self):
        return self._get_current_io().errors

    @property
    def line_buffering(self):
        return self._get_current_io().line_buffering

    @property
    def buffer(self):
        return self._get_current_io().buffer

    def seekable(self):
        return self._get_current_io().seakable()

    def readable(self):
        return self._get_current_io().readable()

    def writable(self):
        return self._get_current_io().writable()

    def flush(self):
        return self._get_current_io().flush()

    def close(self):
        return self._get_current_io().close()

    @property
    def closed(self):
        return self._get_current_io().closed

    @property
    def name(self):
        return self._get_current_io().name

    def fileno(self):
        return self._get_current_io().fileno()

    def isatty(self):
        return self._get_current_io().isatty()

    def tell(self):
        return self._get_current_io().tell()

    def seek(self, cookie, whence=0):
        return self._get_current_io().seek(cookie, whence)
