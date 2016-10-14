namespace Python.Bootstrapper
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.IO.Compression;
    using System.Reflection;

    using Mono.Unix.Native;

    /// <summary>
    /// Bootstrapper for python.runtime.dll library.
    /// </summary>
    public static class PythonRuntimeBootstrapper
    {
        private static readonly object PythonRuntimeAssemblyLock = new object();

        private static Assembly _pythonRuntimeAssembly;

        private static byte[] _pythonRuntimeAssemblyContent;

        /// <summary>
        /// Register loader for correct python.runtime.dll (according to OS, python version, python build options, etc.).
        /// </summary>
        /// <param name="allowDeleteDefaultLib">Allow to delete Python.Runtime.dll from bin dir to avoid wrong runtime load by CLR.</param>
        /// <exception cref="InvalidDataException">Python.runtime.dll found in the bin directory.</exception>
        /// <exception cref="InvalidOperationException">Python detection error.</exception>
        /// <exception cref="FileNotFoundException">Error loading file for the detected python.</exception>
        /// <returns>Strict dll name required for detected python.</returns>
        public static string DetectPythonAndRegisterRuntime(bool allowDeleteDefaultLib = false)
        {
            if (allowDeleteDefaultLib)
            {
                var defaultLibFullPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Python.Runtime.dll");
                if (File.Exists(defaultLibFullPath))
                {
                    File.Delete(defaultLibFullPath);
                }
            }

            EnsurePythonRuntimeDllNotInBin();

            string os;
            List<string> libraryPathes;
            string dllName = DetectRequiredPythonRuntimDll(out os, out libraryPathes);

            try
            {
                _pythonRuntimeAssemblyContent = LoadRequiredAssembly(dllName);
            }
            catch (Exception ex)
            {
                throw new FileNotFoundException($"Error loading {dllName} from Python.Runtime.zip: {ex.Message}");
            }

            AppDomain.CurrentDomain.AssemblyResolve += AssemblyResolveHandler;
            var pythonRuntimeType =
                Type.GetType(
                    "Python.Runtime.Runtime, Python.Runtime, Version=4.0.0.1, Culture=neutral, PublicKeyToken=null",
                    true);

            if (os == "elf")
            {
                MakeLibSymLink(pythonRuntimeType, libraryPathes);
            }

            return dllName;
        }

        private static Assembly AssemblyResolveHandler(object sender, ResolveEventArgs args)
        {
            if (args.Name.StartsWith("Python.Runtime"))
            {
                lock (PythonRuntimeAssemblyLock)
                {
                    if (_pythonRuntimeAssembly == null)
                    {
                        _pythonRuntimeAssembly = Assembly.Load(_pythonRuntimeAssemblyContent);
                    }

                    return _pythonRuntimeAssembly;
                }
            }

            return null;
        }

        private static string DetectRequiredPythonRuntimDll(out string os, out List<string> librariesPathElements)
        {
            string stdOut;
            string pythonInfoFile = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "python-info.tmp");

            if (0
                == Stdlib.system(
                    $@"python -c ""import sys; print(sys.version_info.major); print(sys.version_info.minor); import array; print(array.array('u').itemsize); import platform; print(platform.architecture()[0]); print(platform.architecture()[1]); import sysconfig; print(sysconfig.get_config_vars('WITH_PYMALLOC')); print(sysconfig.get_config_var('LIBPL'));print(sysconfig.get_config_var('LIBDIR'))"" > {pythonInfoFile}"))
            {
                if (File.Exists(pythonInfoFile))
                {
                    stdOut = File.ReadAllText(pythonInfoFile);

                    File.Delete(pythonInfoFile);
                }
                else
                {
                    throw new InvalidOperationException("Failed to execute python");
                }
            }
            else
            {
                throw new InvalidOperationException("Failed to execute python");
            }

            var result = stdOut.Split('\n');
            int majorVersion;
            int minorVersion;
            int charSize;
            bool pyMalloc = false;

            if (result.Length >= 8)
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

            os = "elf";
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

            librariesPathElements = new List<string>();
            for (int i = 0; i < result.Length; i++)
            {
                var libPathElement = result[i].Trim();
                if (libPathElement != "None" && libPathElement != string.Empty)
                {
                    librariesPathElements.Add(libPathElement);
                }
            }

            string dllName = "Python.Runtime";
            dllName += "-" + os;
            dllName += "-" + result[3].Substring(0, 2);
            dllName += "-ucs" + charSize;

            dllName += "-" + result[0] + result[1] + options;

            dllName += ".dll";
            return dllName;
        }

        private static void EnsurePythonRuntimeDllNotInBin()
        {
            if (File.Exists(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Python.Runtime.dll")))
            {
                throw new InvalidDataException(
                          "Python.Runtime.dll found in the application directory. It's not supported and will cause Python.Net library crash.");
            }
        }

        /// <summary>
        /// Loads content of required assembly.
        /// </summary>
        /// <param name="requiredAssemblyName">Required assembly file name.</param>
        /// <returns>Assembly file content.</returns>
        private static byte[] LoadRequiredAssembly(string requiredAssemblyName)
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

        private static void MakeLibSymLink(Type pythonLoaderType, List<string> librariesPathElements)
        {
            var makeSymLinkLibMethodInfo = pythonLoaderType.GetMethod(
                "MakeLibSymLink",
                BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);

            makeSymLinkLibMethodInfo.Invoke(null, new object[] { librariesPathElements });
        }

        private static string NormalizeOutput(string str)
        {
            return str.Trim().Replace("\r\n", "\n").Replace("\n\r", "\n");
        }
    }
}