namespace Python.Net
{
    using System;

    using JetBrains.Annotations;

    /// <summary>
    /// Python output capturing fame.
    /// </summary>
    public interface IPythonOutputCapturingFrame : IDisposable
    {
        /// <summary>
        /// Reads text from python stderr and cleans buffer.
        /// </summary>
        /// <returns>Currently captured stderr.</returns>
        [NotNull]
        string ReadStdErr();

        /// <summary>
        /// Reads text from python stdout and cleans buffer.
        /// </summary>
        /// <returns>Currently captured stdout.</returns>
        [NotNull]
        string ReadStdOut();
    }
}