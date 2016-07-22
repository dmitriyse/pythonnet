namespace Python.Config
{
    using System;
    using System.Configuration;
    using System.IO;
    using System.Reflection;
    using System.Text.RegularExpressions;

    /// <summary>
    /// Python.Net runtime configuration.
    /// </summary>
    public static class PythonConfig
    {
        private static object _assemblyLoadLock = new object();

        private static Exception _configException;

        private static Assembly _pythonRuntimeAssembly;

        private static string _pythonVersion;

        private static Regex _versionNumberRegex = new Regex("^(?<major>\\d)\\.(?<minor>\\d)$");

        /// <summary>
        /// Initializes static members of the <see cref="PythonConfig"/> class. 
        /// </summary>
        static PythonConfig()
        {
            if (IntPtr.Size == 4)
            {
                throw new NotSupportedException("32 bit platform not supported by Python.Config library.");
            }
            AppDomain.CurrentDomain.AssemblyResolve += AssemblyResolveHandler;
        }

        /// <summary>
        /// Loaded assembly name.
        /// </summary>
        public static string LoadedRuntimeAssembly { get; private set; }

        /// <summary>
        /// Pyhon runtime version. Can be changed only 
        /// </summary>
        public static string PythonVersion
        {
            get
            {
                if (_pythonVersion == null)
                {
                    try
                    {
                        var pythonConfigSection = (PythonConfigSection)ConfigurationManager.GetSection("pythonConfig");
                        _pythonVersion = pythonConfigSection.PythonVersion;

                        ValidatePythonVersion(_pythonVersion);
                    }
                    catch (Exception ex)
                    {
                        _pythonVersion = "2.7";
                        _configException = ex;
                        AppDomain.CurrentDomain.SetData("PythonConfigException", _configException);
                    }
                }

                return _pythonVersion;
            }

            private set
            {
                // Disabling this feature. Dynamic version select should be implemented through callback in configuration file.
                if (IsRuntimeAssemblyLoaded)
                {
                    throw new InvalidOperationException(
                              "Python version can be changed only before Python.Runtime assembly loaded by CLR.");
                }

                ValidatePythonVersion(value);
                _pythonVersion = value;
            }
        }

        /// <summary>
        /// Used internally to determine that Python.Runtime assembly already loaded.
        /// </summary>
        internal static bool IsRuntimeAssemblyLoaded { get; set; }

        /// <summary>
        /// Forces Python Config library to be initialized.
        /// </summary>
        public static void EnsureInitialized()
        {
            // Do nothing, but forces CLR to load PythonConfig type.
        }

        private static Assembly AssemblyResolveHandler(object sender, ResolveEventArgs args)
        {
            if (args.Name.StartsWith("Python.Runtime"))
            {
                lock (_assemblyLoadLock)
                {
                    if (_pythonRuntimeAssembly != null)
                    {
                        return _pythonRuntimeAssembly;
                    }

                    string platform = Path.DirectorySeparatorChar == '\\' ? "Win64" : "Linux64";

                    // We will load assembly here.
                    string resourceName = $"Python.Runtime-Py{PythonVersion.Replace(".", string.Empty)}-{platform}.dll";

                    // looks for the assembly from the resources and load it
                    using (var stream = typeof(PythonConfig).Assembly.GetManifestResourceStream(resourceName))
                    {
                        if (stream != null)
                        {
                            byte[] assemblyData = new byte[stream.Length];
                            stream.Read(assemblyData, 0, assemblyData.Length);
                            try
                            {
                                _pythonRuntimeAssembly = Assembly.Load(assemblyData);
                                LoadedRuntimeAssembly = resourceName;
                                return _pythonRuntimeAssembly;
                            }
                            catch (Exception ex)
                            {
                                AppDomain.CurrentDomain.SetData("PythonConfigException", ex);
                            }
                        }
                    }
                }
            }

            return null;
        }

        private static void ValidatePythonVersion(string version)
        {
            if (!_versionNumberRegex.Match(version).Success)
            {
                throw new ArgumentException("Python version should be specified in x.y, for example \"2.7\".");
            }

            if (version != "2.7" && version != "3.5")
            {
                throw new ArgumentException("Unsupported python version. Only 2.7 and 3.5 are supported.");
            }
        }
    }
}