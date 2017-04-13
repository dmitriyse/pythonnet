namespace Python.Net
{
    using System;

    using ClrCoder.Logging;
    using ClrCoder.Logging.Std;

    using JetBrains.Annotations;

    using Runtime;

    /// <summary>
    /// Extension methods related to <see cref="PythonWrapper"/> .
    /// </summary>
    [PublicAPI]
    public static class PythonWrapperExtensions
    {
        /// <summary>
        /// Writes log debug log entry with python output content.
        /// </summary>
        /// <param name="logger">Logger to write logs to.</param>
        /// <param name="outputFrame">Output frame where logs was collected.</param>
        public static void DebugPythonOutput(
            [NotNull] this IJsonLogger logger,
            [NotNull] IPythonOutputCapturingFrame outputFrame)
        {
            string stdOut = outputFrame.ReadStdOut();
            if (!string.IsNullOrWhiteSpace(stdOut))
            {
                logger.Debug(stdOut, (_, str) => _($"PyOut:\n{str}"));
            }

            string stdErr = outputFrame.ReadStdOut();
            if (!string.IsNullOrWhiteSpace(stdErr))
            {
                logger.Debug(stdErr, (_, str) => _($"PyErr:\n{str}"));
            }
        }

        /// <summary>
        /// Wraps <see cref="PythonNetException"/> exception.
        /// </summary>
        /// <param name="exception">Python exception to wrap.</param>
        /// <returns>Wrapped exception.</returns>
        public static PythonNetException WrapAndDispose(this PythonException exception)
        {
            if (exception == null)
            {
                throw new ArgumentNullException(nameof(exception));
            }

            var result = new PythonNetException(exception);
            exception.Dispose();
            return result;
        }
    }
}
