namespace Python.Net
{
    using System;

    using JetBrains.Annotations;
#if NET46
    using System.Runtime.Serialization;
#endif

    /// <summary>
    /// Exception with binding Python code to C# wrappers.
    /// </summary>
    public class PythonBindingException : Exception
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="PythonBindingException"/> class.
        /// </summary>
        /// <param name="message">Error message.</param>
        public PythonBindingException([NotNull] string message)
            : base(message)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="PythonBindingException"/> class.
        /// </summary>
        /// <param name="message">Error message.</param>
        /// <param name="innerException">Inner exception.</param>
        public PythonBindingException([NotNull] string message, [NotNull] Exception innerException)
            : base(message, innerException)
        {
        }
#if NET46
        /// <summary>
        /// Initializes a new instance of the <see cref="PythonBindingException"/> class with serialized data.
        /// </summary>
        /// <param name="info">
        /// The <see cref="T:System.Runtime.Serialization.SerializationInfo"/> that holds the serialized object
        /// data about the exception being thrown.
        /// </param>
        /// <param name="context">
        /// The <see cref="T:System.Runtime.Serialization.StreamingContext"/> that contains contextual
        /// information about the source or destination.
        /// </param>
        /// <exception cref="T:System.ArgumentNullException">The <paramref name="info"/> parameter is null. </exception>
        /// <exception cref="T:System.Runtime.Serialization.SerializationException">
        /// The class name is null or
        /// <see cref="P:System.Exception.HResult"/> is zero (0).
        /// </exception>
        public PythonBindingException([NotNull] SerializationInfo info, StreamingContext context)
            : base(info, context)
        {
        }
#endif
    }
}