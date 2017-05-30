namespace Python.EmbeddingTest
{
    using ClrCoder.Logging.Std;
    using ClrCoder.Threading;

    using Net;

    using NUnit.Framework;

    /// <summary>
    /// Manages environment for all tests.
    /// </summary>
    [SetUpFixture]
    public class TestsEnvironment
    {
        private PythonWrapper _wrapper;

        /// <summary>
        /// Initializes tests environment.
        /// </summary>
        [OneTimeSetUp]
        public void Init()
        {
            var testOutLogger = new TextJsonLogger(SyncHandler.Instance, TestContext.WriteLine);
            _wrapper = new PythonWrapper(testOutLogger);
        }

        /// <summary>
        /// Shutdown tests environment.
        /// </summary>
        [OneTimeTearDown]
        public void Shutdown()
        {
            _wrapper.Dispose();
        }
    }
}
