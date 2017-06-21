namespace Python.Net
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.IO;
    using System.Reflection;
    using System.Runtime.CompilerServices;
    using System.Threading;

    using ClrCoder;
    using ClrCoder.Diagnostics;
    using ClrCoder.Logging;
    using ClrCoder.Logging.Std;
    using ClrCoder.Threading;

    using JetBrains.Annotations;

    using Runtime;

    /// <summary>
    /// Encapsulates python wrapping issues.
    /// </summary>
    [PublicAPI]
    public class PythonWrapper : IDisposable
    {
        [CanBeNull]
        [ThreadStatic]
        private static PyGIL _curGIL;

        [CanBeNull]
        private static PythonWrapper _instance;

        private readonly IntPtr _multiThreadedPtr;

        [NotNull]
        private readonly Dictionary<string, dynamic> _modulesCache = new Dictionary<string, dynamic>();

        [NotNull]
        private readonly HashSet<string> _registeredPathes = new HashSet<string>();

        [NotNull]
        private readonly dynamic _ioModule;

        [NotNull]
        private readonly dynamic _sysModule;

        [NotNull]
        private readonly dynamic _builtinsModule;

        [NotNull]
        private readonly dynamic _pythonnetModule;

        private bool _disposed;

        /// <summary>
        /// Initializes a new instance of the <see cref="PythonWrapper"/> class.
        /// </summary>
        /// <param name="logger">Logger, by default used console logger.</param>
        public PythonWrapper([CanBeNull] IJsonLogger logger = null)
        {
            // Skipping initialization if already initialized.
            if (Interlocked.CompareExchange(ref _instance, this, null) == this)
            {
                throw new InvalidOperationException("Python wraper was already initialized.");
            }

            if (logger == null)
            {
                logger = new ConsoleJsonLogger(SyncHandler.Instance);
            }

            Log = new ClassJsonLogger<PythonWrapper>(logger);

            Log.Debug(_ => _("Python engine initialization started."));

            string pythonetModuleFileName = Path.Combine(AppContext.BaseDirectory, "pythonnet.py");

            // Ensuring that pythonnet.py file exists in the bin directory
            if (!File.Exists(pythonetModuleFileName) || new FileInfo(pythonetModuleFileName).Length == 0)
            {
                Assembly assembly = typeof(PythonWrapper).GetTypeInfo().Assembly;
                using (Stream stream = assembly.GetManifestResourceStream("Python.Net.pythonnet.py"))
                using (Stream file = File.Create(pythonetModuleFileName))
                {
                    stream.CopyTo(file);
                }
            }

            using (GIL())
            {
                _sysModule = SafeLoadModuleInternal("sys", false);
                _ioModule = SafeLoadModuleInternal("io", false, ensureMethods: new[] { "StringIO" });
                _sysModule.path.append(AppContext.BaseDirectory);
                _pythonnetModule = SafeLoadModuleInternal("pythonnet", false);
                _sysModule.stdout = _pythonnetModule.ThreadedDemuxerTextIO(_sysModule.stdout);
                _sysModule.stderr = _pythonnetModule.ThreadedDemuxerTextIO(_sysModule.stderr);
                Log.Info((string)_sysModule.version, (_, ver) => _($"Initialized Python Engine: {ver}"));
                _builtinsModule = SafeLoadModuleInternal("builtins", false);
            }

            _multiThreadedPtr = PythonEngine.BeginAllowThreads();
        }

        /// <summary>
        /// Finalizes an instance of the <see cref="PythonWrapper"/> class.
        /// </summary>
        ~PythonWrapper()
        {
            _disposed = true;

            // On mono this method will be called.
            Dispose(false);
        }

        /// <summary>
        /// Executed before python engine shutdown.
        /// </summary>
        public static event EventHandler BeforeShutdown;

        /// <summary>
        /// Gets builtins module.
        /// </summary>
        [NotNull]
        public static dynamic BuiltinsModule
        {
            get
            {
                VerifyGIL();
                return _instance._builtinsModule;
            }
        }

        [NotNull]
        private IJsonLogger Log { get; }

        /// <summary>
        /// Opens python <see cref="PythonWrapper.GIL"/> lock.
        /// </summary>
        /// <param name="callerFilePath">Call file path.</param>
        /// <param name="callerMemberName">Call member name.</param>
        /// <param name="callerLineNumber">Call line number.</param>
        /// <returns>Token for release <see cref="PythonWrapper.GIL"/> lock.</returns>
        [NotNull]
        public static IDisposable GIL(
            [NotNull] [CallerFilePath] string callerFilePath = "",
            [NotNull] [CallerMemberName] string callerMemberName = "",
            [CallerLineNumber] int callerLineNumber = 0)
        {
            if (callerFilePath == null)
            {
                throw new ArgumentNullException(nameof(callerFilePath));
            }

            if (callerMemberName == null)
            {
                throw new ArgumentNullException(nameof(callerMemberName));
            }

            return new PyGIL(callerFilePath, callerMemberName, callerLineNumber);
        }

        /// <summary>
        /// Opens output capturing frame.
        /// </summary>
        /// <param name="callerFilePath">Call file path.</param>
        /// <param name="callerMemberName">Call member name.</param>
        /// <param name="callerLineNumber">Call line number.</param>
        /// <returns>Python output capturing frame.</returns>
        [NotNull]
        public static IPythonOutputCapturingFrame PushOutputCapturing(
            [NotNull] [CallerFilePath] string callerFilePath = "",
            [NotNull] [CallerMemberName] string callerMemberName = "",
            [CallerLineNumber] int callerLineNumber = 0)
        {
            if (callerFilePath == null)
            {
                throw new ArgumentNullException(nameof(callerFilePath));
            }

            if (callerMemberName == null)
            {
                throw new ArgumentNullException(nameof(callerMemberName));
            }

            VerifyGIL();
            return new OutputCapturingFrame(callerFilePath, callerMemberName, callerLineNumber);
        }

        /// <summary>
        /// Load module or return cached loaded module.
        /// </summary>
        /// <remarks>
        /// Assumed that this method will be called from inside <see cref="PythonWrapper.GIL"/> lock.
        /// </remarks>
        /// <param name="moduleName">Module to load.</param>
        /// <param name="modulePath">Path where module is located.</param>
        /// <param name="versionAttribute">Attribute that contains version string. This is helpfull for logging.</param>
        /// <param name="ensureMethods">Ensures that module have specified methods.</param>
        /// <exception cref="System.Runtime.InteropServices.ExternalException">Module load error.</exception>
        /// <returns>Loaded module or null, if load failed.</returns>
        [NotNull]
        public static PyObject SafeLoadModule(
            [NotNull] string moduleName,
            [CanBeNull] string modulePath = null,
            [NotNull] string versionAttribute = "__version__",
            [CanBeNull] [ItemNotNull] IReadOnlyCollection<string> ensureMethods = null)
        {
            if (moduleName == null)
            {
                throw new ArgumentNullException(nameof(moduleName));
            }

            if (versionAttribute == null)
            {
                throw new ArgumentNullException(nameof(versionAttribute));
            }

            VerifyGIL();

            // ReSharper disable once PossibleNullReferenceException
            return _instance.SafeLoadModuleInternal(
                moduleName,
                true,
                modulePath: modulePath,
                versionAttribute: versionAttribute,
                ensureMethods: ensureMethods);
        }

        /// <summary>
        /// Checks that engine initialized and healthy.
        /// </summary>
        public static void VerifyEngineInitialized()
        {
            if (_instance == null)
            {
                throw new InvalidOperationException("Python engine was not initialized.");
            }

            if (_instance._disposed)
            {
                throw new InvalidOperationException("Python engine was disposed.");
            }
        }

        private static void OnBeforeShutdown()
        {
            BeforeShutdown?.Invoke(null, EventArgs.Empty);
        }

        private static void VerifyEngineAlive()
        {
            // ReSharper disable once PossibleNullReferenceException
            if (_instance._disposed)
            {
                throw new InvalidOperationException("Python engine was disposed.");
            }
        }

        private static void VerifyGIL()
        {
            VerifyEngineInitialized();
            if (_curGIL == null)
            {
                throw new InvalidOperationException("This operation can be performed only under GIL lock.");
            }
        }

        /// <inheritdoc/>
        public void Dispose()
        {
            try
            {
                OnBeforeShutdown();
            }
            catch
            {
                // Mute errors.
            }

            _disposed = true;
            GC.SuppressFinalize(this);
            Dispose(true);
        }

        private void Dispose(bool disposeManaged)
        {
            if (disposeManaged)
            {
                Log.Debug(_ => _("Python engine gracefull shutdown started."));
            }
            else
            {
                Log.Error(_ => _("Buggy python engine shutdown. Probably application will crash."));
            }

            try
            {
                if (_multiThreadedPtr != IntPtr.Zero)
                {
                    PythonEngine.EndAllowThreads(_multiThreadedPtr);
                }
            }
            catch (Exception ex)
            {
                // TODO: Add exception to message. (.Exception(exception))
                Log.Error(ex, (_, exception) => _($"Multitheaded environment shutdown error"));
            }

            PythonEngine.Shutdown();
        }

        [NotNull]
        private PyObject SafeLoadModuleInternal(
            [NotNull] string moduleName,
            bool logModuleImport,
            [CanBeNull] string modulePath = null,
            [NotNull] string versionAttribute = "__version__",
            [CanBeNull] [ItemNotNull] IReadOnlyCollection<string> ensureMethods = null)
        {
            var requirePathRegister = false;

            string normalizedPath = null;

            // ReSharper disable once PossibleNullReferenceException
            lock (_instance)
            {
                if (modulePath != null)
                {
                    normalizedPath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, modulePath));
                    requirePathRegister = !_registeredPathes.Contains(normalizedPath);
                }

                dynamic cachedModule = _modulesCache.GetOrDefault(moduleName);
                if (cachedModule != null)
                {
                    return cachedModule;
                }
            }

            if (requirePathRegister)
            {
                dynamic sysModule = SafeLoadModuleInternal("sys", logModuleImport);
                sysModule.path.append(normalizedPath);
                lock (_instance)
                {
                    _registeredPathes.Add(normalizedPath);
                }
            }

            IPythonOutputCapturingFrame outputFrame = null;
            if (logModuleImport)
            {
                outputFrame = PushOutputCapturing();
            }

            try
            {
                PyObject module = PythonEngine.ImportModule(moduleName);

                // ReSharper disable once ConditionIsAlwaysTrueOrFalse
                if (module != null)
                {
                    lock (_instance)
                    {
                        _modulesCache[moduleName] = module;
                    }

                    if (logModuleImport)
                    {
                        string versionStr = null;
                        if (module.HasAttr(versionAttribute))
                        {
                            versionStr = module.GetAttr(versionAttribute)?.ToString();
                        }

                        string moduleVersion = versionStr == null ? string.Empty : $", version: {versionStr}";
                        string stdOut = outputFrame.ReadStdOut();
                        string stdErr = outputFrame.ReadStdErr();
                        string stringLogMessage = $"Module {moduleName} loaded{moduleVersion}";

                        Log.Info(_ => _(stringLogMessage).Data(new { PyOut = stdOut, PyErr = stdErr }));
                    }
                }
                else
                {
                    // PythonException internally captures real python module load error.
                    var pythonException = new PythonException();
                    throw pythonException;
                }

                if (ensureMethods != null)
                {
                    foreach (string methodName in ensureMethods)
                    {
                        if (!module.HasAttr(methodName))
                        {
                            throw new PythonBindingException(
                                $"Module {moduleName} does not contains method {methodName}.");
                        }
                    }
                }

                return module;
            }
            catch (PythonException ex)
            {
                if (logModuleImport)
                {
                    string stdOut = outputFrame.ReadStdOut();
                    string stdErr = outputFrame.ReadStdErr();

                    Log.Warning(
                        moduleName,
                        (_, name) => _($"Failed to load module: {name}").Data(new { PyOut = stdOut, PyErr = stdErr }));
                }

                throw new PythonBindingException($"Failed to import python module {moduleName}", ex.WrapAndDispose());
            }
            finally
            {
                outputFrame?.Dispose();
            }
        }

        private class OutputCapturingFrame : IPythonOutputCapturingFrame
        {
            [CanBeNull]
            private readonly OutputCapturingFrame _parent;

            [CanBeNull]
            private OutputCapturingFrame _inner;

            [CanBeNull]
            private dynamic _originalStdOut;

            [CanBeNull]
            private dynamic _originalStdErr;

            [NotNull]
            private dynamic _stdOut;

            [NotNull]
            private dynamic _stdErr;

            public OutputCapturingFrame(
                [NotNull] string callerFilePath,
                [NotNull] string callerMemberName,
                int callerLineNumber = 0)
            {
                VerifyEngineAlive();

                CallerFilePath = callerFilePath;
                CallerMemberName = callerMemberName;
                CallerLineNumber = callerLineNumber;

                Debug.Assert(_curGIL != null, "_curGIL != null");
                Debug.Assert(_instance != null, "_instance != null");

                _parent = _curGIL.CurFrame;
                if (_parent != null)
                {
                    _parent._inner = this;
                }
                else
                {
                    _originalStdOut = _instance._sysModule.stdout.cur_thread_io;
                    _originalStdErr = _instance._sysModule.stderr.cur_thread_io;
                }

                _stdOut = _instance._ioModule.StringIO();
                _stdErr = _instance._ioModule.StringIO();

                _instance._sysModule.stdout.cur_thread_io = _stdOut;
                _instance._sysModule.stderr.cur_thread_io = _stdErr;
            }

            /// <summary>
            /// Finalizes an instance of the <see cref="PythonWrapper.OutputCapturingFrame"/> class.
            /// </summary>
            ~OutputCapturingFrame()
            {
                Debug.Fail(
                    $"Python output capturing frame was not disposed. File={CallerFilePath}, Method={CallerMemberName}, Line={CallerLineNumber}");
            }

            [NotNull]
            public string CallerFilePath { get; set; }

            [NotNull]
            public string CallerMemberName { get; set; }

            public int CallerLineNumber { get; set; }

            public void Dispose()
            {
                GC.SuppressFinalize(this);
                if (_inner != null)
                {
                    string innerLockStr =
                        $"File={_inner.CallerFilePath}, Method={_inner.CallerMemberName}, Line={_inner.CallerLineNumber}";

                    Debug.Fail(
                        $"Inner python output frame was not disposed. File={CallerFilePath}, Method={CallerMemberName}, Line={CallerLineNumber}\nInner frame:\n{innerLockStr}");
                }

                Debug.Assert(_instance != null, "_instance != null");
                Debug.Assert(_curGIL != null, "_curGIL != null");

                _curGIL.CurFrame = _parent;

                if (_parent != null)
                {
                    _parent._inner = null;
                    _instance._sysModule.stdout.cur_thread_io = _parent._stdOut;
                    _instance._sysModule.stderr.cur_thread_io = _parent._stdErr;
                }
                else
                {
                    _instance._sysModule.stdout.cur_thread_io = _originalStdOut ?? PyObject.FromManagedObject(null);
                    _instance._sysModule.stderr.cur_thread_io = _originalStdErr ?? PyObject.FromManagedObject(null);
                }
            }

            public string ReadStdErr()
            {
                VerifyEngineAlive();

                Debug.Assert(_instance != null, "_instance != null");

                _instance._sysModule.stderr.cur_thread_io = _instance._ioModule.StringIO();
                dynamic result = _stdErr.getvalue();
                _stdErr.close();
                _stdErr = _instance._sysModule.stderr.cur_thread_io;

                return result;
            }

            public string ReadStdOut()
            {
                VerifyEngineAlive();

                Debug.Assert(_instance != null, "_instance != null");

                _instance._sysModule.stdout.cur_thread_io = _instance._ioModule.StringIO();
                dynamic result = _stdOut.getvalue();
                _stdOut.close();
                _stdOut = _instance._sysModule.stdout.cur_thread_io;

                return result;
            }
        }

        private class PyGIL : IDisposable
        {
            [CanBeNull]
            private readonly PyGIL _parent;

            [NotNull]
            private readonly string _callerFilePath;

            [NotNull]
            private readonly string _callerMemberName;

            private readonly int _callerLineNumber;

            [CanBeNull]
            private readonly IDisposable _pythonNetGIL;

            [CanBeNull]
            private PyGIL _inner;

            public PyGIL(
                [NotNull] string callerFilePath,
                [NotNull] string callerMemberName,
                int callerLineNumber = 0)
            {
                VerifyEngineInitialized();

                _parent = _curGIL;
                _curGIL = this;

                if (_parent == null)
                {
                    var gilTimer = CodeTimer.Start(3);
                    _pythonNetGIL = Py.GIL();
                    if (gilTimer.Time > 1.0)
                    {
                        // ReSharper disable once PossibleNullReferenceException
                        _instance.Log.Warning(_=>_("GIL takes too long"));
                    }
                }
                else
                {
                    _parent._inner = this;
                }

                _callerFilePath = callerFilePath;
                _callerMemberName = callerMemberName;
                _callerLineNumber = callerLineNumber;
            }

            /// <summary>
            /// Finalizes an instance of the <see cref="PythonWrapper.PyGIL"/> class.
            /// </summary>
            ~PyGIL()
            {
                Debug.Fail(
                    $"Python GIL lock was not disposed. File={_callerFilePath}, Method={_callerMemberName}, Line={_callerLineNumber}");
            }

            /// <summary>
            /// Current output capturing frame.
            /// </summary>
            [CanBeNull]
            public OutputCapturingFrame CurFrame { get; set; }

            public void Dispose()
            {
                GC.SuppressFinalize(this);
                if (_inner != null)
                {
                    string innerLockStr =
                        $"File={_inner._callerFilePath}, Method={_inner._callerMemberName}, Line={_inner._callerLineNumber}";

                    Debug.Fail(
                        $"GIL lock disposed, while inner locks captured File={_callerFilePath}, Method={_callerMemberName}, Line={_callerLineNumber}\nInner lock:\n{innerLockStr}");
                }

                if (CurFrame != null)
                {
                    string outputFrameStr =
                        $"File={CurFrame.CallerFilePath}, Method={CurFrame.CallerMemberName}, Line={CurFrame.CallerLineNumber}";

                    Debug.Fail(
                        $"GIL lock disposed, while inner output frame was not, File={_callerFilePath}, Method={_callerMemberName}, Line={_callerLineNumber}\nOutput Frame:\n{outputFrameStr}");
                }

                _curGIL = _parent;
                if (_parent == null)
                {
                    _pythonNetGIL?.Dispose();
                }
                else
                {
                    _parent._inner = null;
                }
            }
        }
    }
}
