namespace Python.Bootstrapper.Test
{
    using FluentAssertions;

    using NUnit.Framework;

    [TestFixture]
    public class BootstrapperTests
    {
        [Test]
        [Ignore("For debug")]
        public void DetectRequiredRuntimeDllTest()
        {
            var dll = PythonRuntimeBootstrapper.DetectRequiredPythonRuntimDll();
            dll.Should().Be("Python.Runtime-win-64-ucs2.dll");
        }

        [Test]
        [Ignore("For debug")]
        public void LoadAssemblyTest()
        {
            var dllContent = PythonRuntimeBootstrapper.LoadRequiredAssembly("SomeFile.txt");
            dllContent.Length.Should().BePositive();

            dllContent = PythonRuntimeBootstrapper.LoadRequiredAssembly("SomeNonZipFile.txt");
            dllContent.Length.Should().BePositive();
        }
    }
}