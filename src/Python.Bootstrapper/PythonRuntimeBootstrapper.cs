namespace Python.Bootstrapper
{
    using System;
    using System.Diagnostics;
    using System.IO;
    using System.IO.Compression;
    using System.Reflection;

    /// <summary>
    /// Bootstrapper for python.runtime.dll library.
    /// </summary>
    public static class PythonRuntimeBootstrapper
    {
        private static byte[] _pythonRuntimeAssembly;

        /// <summary>
        /// Register loader for correct python.runtime.dll (according to OS, python version, python build options, etc.).
        /// </summary>
        /// <param name="pythonLibPath"></param>
        /// <exception cref="InvalidDataException">Python.runtime.dll found in the bin directory.</exception>
        /// <exception cref="InvalidOperationException">Python detection error.</exception>
        /// <exception cref="FileNotFoundException">Error loading file for the detected python.</exception>
        /// <returns>Strict dll name required for detected python.</returns>
        public static string DetectPythonAndRegisterRuntime(string pythonLibPath = null)
        {
            EnsurePythonRuntimeDllNotInBin();

            string dllName = DetectRequiredPythonRuntimDll();

            try
            {
                _pythonRuntimeAssembly = LoadRequiredAssembly(dllName);
            }
            catch (Exception ex)
            {
                throw new FileNotFoundException($"Error loading {dllName} from Python.Runtime.zip: {ex.Message}");
            }

            AppDomain.CurrentDomain.AssemblyResolve += AssemblyResolveHandler;
            var pythonRuntimeType = Type.GetType("Python.Runtime.Runtime, Python.Runtime, Version=4.0.0.1, Culture=neutral, PublicKeyToken=null", true);

            return dllName;
        }

        private static Assembly AssemblyResolveHandler(object sender, ResolveEventArgs args)
        {
            if (args.Name.StartsWith("Python.Runtime"))
            {
                return Assembly.Load(_pythonRuntimeAssembly);
            }

            return null;
        }

        /// <summary>
        /// Detects required runtime assembly file name.
        /// </summary>
        /// <returns>Name of required runtime assembly file name.</returns>
        internal static string DetectRequiredPythonRuntimDll()
        {
            try
            {
                var pythonTestProcess = new Process
                                            {
                                                StartInfo =
                                                    new ProcessStartInfo
                                                        {
                                                            FileName = "python",
                                                            Arguments =
                                                                @"-c ""import sys; print(sys.version_info.major); print(sys.version_info.minor); import array; print(array.array('u').itemsize); import platform; print(platform.architecture()[0]); print(platform.architecture()[1]); import sysconfig; print(sysconfig.get_config_vars('WITH_PYMALLOC'))""",
                                                            UseShellExecute = false,
                                                            RedirectStandardOutput = true,
                                                            RedirectStandardError = true,
                                                            RedirectStandardInput = true
                                                        }
                                            };
                pythonTestProcess.Start();
                pythonTestProcess.WaitForExit();
                var stdOut = NormalizeOutput(pythonTestProcess.StandardOutput.ReadToEnd());
                var stdErr = NormalizeOutput(pythonTestProcess.StandardError.ReadToEnd());
                if (!string.IsNullOrEmpty(stdErr))
                {
                    throw new InvalidOperationException($"Failed to extract information about python: {stdErr}");
                }

                var result = stdOut.Split('\n');
                int majorVersion;
                int minorVersion;
                int charSize;
                bool pyMalloc = false;

                if (result.Length == 6)
                {
                    if (int.TryParse(result[0], out majorVersion) && int.TryParse(result[1], out minorVersion)
                        && int.TryParse(result[2], out charSize))
                    {
                    }
                    else
                    {
                        throw new InvalidOperationException("Failed to extract information about python.");
                    }

                    if (result[3].Trim().Length != 5)
                    {
                        throw new InvalidOperationException("Failed to extract information about python.");
                    }
                }
                else
                {
                    throw new InvalidOperationException("Failed to extract information about python.");
                }

                string os = "elf";
                result[4] = result[4].Trim();

                if (result[4] == "WindowsPE")
                {
                    os = "win";
                }
                else if (result[4] == "ELF")
                {
                    os = "elf";
                }
                else if (result[4] == string.Empty)
                {
                    os = "osx";
                }

                if (result[5] == "[1]")
                {
                    pyMalloc = true;
                }

                string options = string.Empty;

                if (pyMalloc)
                {
                    options += "m";
                }

                string dllName = "Python.Runtime";
                dllName += "-" + os;
                dllName += "-" + result[3].Substring(0, 2);
                dllName += "-ucs" + charSize;

                if (options != string.Empty)
                {
                    dllName += "-" + options;
                }

                dllName += ".dll";
                return dllName;
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Error launching python: {ex.Message}");
            }
        }

        /// <summary>
        /// Loads content of required assembly.
        /// </summary>
        /// <param name="requiredAssemblyName">Required assembly file name.</param>
        /// <returns>Assembly file content.</returns>
        internal static byte[] LoadRequiredAssembly(string requiredAssemblyName)
        {
            var fullRequiredAssemblyName = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, requiredAssemblyName);
            if (File.Exists(fullRequiredAssemblyName))
            {
                return File.ReadAllBytes(fullRequiredAssemblyName);
            }

            using (
                var archive =
                    new ZipArchive(
                        new FileStream(
                            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Python.Runtime.zip"),
                            FileMode.Open),
                        ZipArchiveMode.Read))
            {
                var entry = archive.GetEntry(requiredAssemblyName);
                if (entry == null)
                {
                    throw new FileNotFoundException($"Cannot find file {requiredAssemblyName} the zip archive.");
                }
                using (var zipStream = entry.Open())
                {
                    var memStream = new MemoryStream();
                    zipStream.CopyTo(memStream);
                    return memStream.ToArray();
                }
            }
        }

        private static void EnsurePythonRuntimeDllNotInBin()
        {
            if (File.Exists(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Python.Runtime.dll")))
            {
                throw new InvalidDataException(
                          "Python.Runtime.dll found in the application directory. It's not supported and will cause Python.Net library crash.");
            }
        }

        private static string NormalizeOutput(string str)
        {
            return str.Trim().Replace("\r\n", "\n").Replace("\n\r", "\n");
        }
    }
}