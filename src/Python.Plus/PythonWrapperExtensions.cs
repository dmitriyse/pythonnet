namespace Python.Net
{
    using ClrCoder.Logging;
    using ClrCoder.Logging.Std;

    using JetBrains.Annotations;

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
            var stdOut = outputFrame.ReadStdOut();
            if (!string.IsNullOrWhiteSpace(stdOut))
            {
                logger.Debug(stdOut, (_, str) => _($"PyOut:\n{str}"));
            }

            var stdErr = outputFrame.ReadStdOut();
            if (!string.IsNullOrWhiteSpace(stdErr))
            {
                logger.Debug(stdErr, (_, str) => _($"PyErr:\n{str}"));
            }
        }
    }
}