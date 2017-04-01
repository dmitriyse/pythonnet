namespace Python.EmbeddingTest
{
    using FluentAssertions;

    using Net;

    using NUnit.Framework;

    /// <summary>
    /// <see cref="PythonWrapper"/> class tests.
    /// </summary>
    [TestFixture]
    public class PythonWrapperTests
    {
        /// <summary>
        /// Console output capturing test.
        /// </summary>
        [Test]
        public void ConsoleCapturingTest()
        {
            using (PythonWrapper.GIL())
            {
                using (var frame = PythonWrapper.PushOutputCapturing())
                {
                    var simpleMessage = "Hello world from python";
                    PythonWrapper.BuiltinsModule.print(simpleMessage);
                    frame.ReadStdOut().Should().Contain(simpleMessage);
                }
            }
        }
    }
}