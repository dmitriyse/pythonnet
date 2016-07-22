namespace Python.Config.Test
{
    using System;

    using Python.Runtime;

    internal class Program
    {
        static Program()
        {
            Console.WriteLine("Starting application...");
            // Required to be placed in the static constructor for Mono.
            PythonConfig.EnsureInitialized();
        }

        [STAThread]
        private static int Main(string[] args)
        {
            // Mono workaround required to fix AssemblyResolve + EntryPoint class bug.
            // Classes that was referenced from EntryPoint class cannot use assemblies resolved through "AssemblyResolve"
            Action monoWorkaround = () =>
                {
                    try
                    {
                        if (PythonConfig.LoadedRuntimeAssembly != null)
                        {
                            Console.WriteLine(
                                $"Python.runtime.dll substituted by {PythonConfig.LoadedRuntimeAssembly}.");
                        }
                        else
                        {
                            Console.WriteLine(
                                $"Python.runtime.dll was loaded from application directory.");
                        }

                        // You should put this initialized only if some component starting to use it before first application configuration file read attempt.
                        // So in rare cases.
                        PythonEngine.Initialize();

                        // Like that.
                        try
                        {
                            using (Py.GIL())
                            {
                                dynamic sysModule = Py.Import("sys");
                                Console.WriteLine("Python engine version:");
                                Console.WriteLine(sysModule.version);
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