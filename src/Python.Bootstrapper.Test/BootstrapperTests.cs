namespace Python.Bootstrapper.Test
{
    using System;

    using FluentAssertions;

    using NUnit.Framework;

    [TestFixture]
    public class BootstrapperTests
    {
        [Test]
        [Ignore("For debug")]
        public void DetectRequiredRuntimeDllTest()
        {
            string os;
            var dll = PythonRuntimeBootstrapper.DetectRequiredPythonRuntimDll(out os);
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

        [Test]
        public void UpdateLdLibraryPathTest()
        {
            PythonRuntimeBootstrapper.ExtendLinuxLibraryPath();
        }
    }
}