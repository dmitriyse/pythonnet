﻿namespace Python.Bootstrapper.Test
{
    using System;
    using System.IO;

    using Runtime;
    public class Program
    {
        static Program()
        {
            Console.WriteLine("Starting application...");
            
            // Required to be placed in the static constructor for Mono.
            var loadedLib = PythonRuntimeBootstrapper.DetectPythonAndRegisterRuntime();
            Console.WriteLine($"{loadedLib} runtime loaded.");
        }
        static int Main(string[] args)
        {
            // Mono workaround required to fix AssemblyResolve + EntryPoint class bug.
            // Classes that was referenced from EntryPoint class cannot use assemblies resolved through "AssemblyResolve"
            Action monoWorkaround = () =>
            {
                try
                {
                    // You should put this initialized only if some component starting to use it before first application configuration file read attempt.
                    // So in rare cases.
                    //// PythonConfig.EnsureInitialized();


                    PythonEngine.Initialize();

                    try
                    {
                        using (Py.GIL())
                        {
                            dynamic sysModule = Py.Import("sys");
                            Console.WriteLine("Python engine version:");
                            Console.WriteLine(sysModule.version);
                        }

                        // This workaround reduces risk of Mono crash.
                        // Problem will be fixed in mono 4.6
                        if (Path.DirectorySeparatorChar == '/' || Type.GetType("Mono.Runtime") != null)
                        {
                            Runtime.Py_Main(3, new[] { "/pcfgtest.exe", "-c", "exit" });
                        }
                        else
                        {
                            // Program will crash if we will try to do this under Windows.
                            //// Runtime.Py_Main(3, new[] { "/pcfgtest.exe", "-c", "exit" });
                        }
                    }
                    finally
                    {
                        PythonEngine.Shutdown();
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex);
                }

            };

            monoWorkaround();

            return 0;
        }
    }
}