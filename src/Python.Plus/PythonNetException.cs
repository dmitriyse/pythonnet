namespace Python.Net
{
    using System;

    using Runtime;

    /// <summary>
    /// Wraps <see cref="PythonException"/> exception.
    /// </summary>
    public class PythonNetException : Exception
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="PythonNetException"/> class.
        /// </summary>
        /// <param name="pythonException">An instance of the <see cref="PythonException"/> class to wrap.</param>
        public PythonNetException(PythonException pythonException)
            : base(pythonException.Message)
        {
            if (pythonException == null)
            {
                throw new ArgumentNullException(nameof(pythonException));
            }

            PyStackTrace = pythonException.StackTrace;
        }

        /// <summary>
        /// Python stack trace.
        /// </summary>
        public string PyStackTrace { get; }
    }
}
