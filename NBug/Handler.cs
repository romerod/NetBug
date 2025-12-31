// --------------------------------------------------------------------------------------------------------------------
// <copyright file="Handler.cs" company="NBug Project">
//   Copyright (c) 2011 - 2013 Teoman Soygul. Licensed under MIT license.
// </copyright>
// --------------------------------------------------------------------------------------------------------------------

namespace NBug
{
	using NBug.Core.Reporting;
	using NBug.Core.Util;
	using NBug.Core.Util.Logging;
	using System;
    using System.IO;
	using System.Runtime.ExceptionServices;
    using System.Runtime.InteropServices;
	using System.Threading;
	using System.Threading.Tasks;
	using System.Windows.Threading;

	public static class Handler
	{
        // Using delegates to make sure that static constructor gets called on delegate access

        private static IntPtr _vectoredHandlerHandle;
        private delegate int VectoredExceptionHandlerDelegate(IntPtr exceptionPointers);
        private static readonly VectoredExceptionHandlerDelegate _vectoredHandlerDelegate = VectoredHandler;

        static Handler()
        {
            // Register a native vectored exception handler to attempt to create a minidump
            // when a corrupted process state (native) exception occurs. This is required
            // because the managed attribute HandleProcessCorruptedStateExceptions is ignored
            // on modern .NET runtimes.
            try
            {
                if (Settings.HandleProcessCorruptedStateExceptions)
                {
                    _vectoredHandlerHandle = AddVectoredExceptionHandler(1, _vectoredHandlerDelegate);
                    Logger.Trace($"Vectored exception handler registered: {_vectoredHandlerHandle}");
                }
            }
            catch (Exception ex)
            {
                Logger.Trace($"Failed to register vectored exception handler: {ex}");
            }
        }

        /// <summary>
        /// Used for handling WPF exceptions bound to the UI thread.
        /// Handles the <see cref="Application.DispatcherUnhandledException"/> events in <see cref="System.Windows"/> namespace.
        /// </summary>
        public static DispatcherUnhandledExceptionEventHandler DispatcherUnhandledException
		{
			get
			{
                return DispatcherUnhandledExceptionHandler;
            }
		}

		/// <summary>
		/// Used for handling WinForms exceptions bound to the UI thread.
		/// Handles the <see cref="Application.ThreadException"/> events in <see cref="System.Windows.Forms"/> namespace.
		/// </summary>
		public static ThreadExceptionEventHandler ThreadException
		{
			get
			{
                return ThreadExceptionHandler;
            }
		}

		/// <summary>
		/// Used for handling general exceptions bound to the main thread.
		/// Handles the <see cref="AppDomain.UnhandledException"/> events in <see cref="System"/> namespace.
		/// </summary>
		public static UnhandledExceptionEventHandler UnhandledException
		{
			get
			{
                return UnhandledExceptionHandler;
            }
		}

		/// <summary>
		/// Used for handling System.Threading.Tasks bound to a background worker thread.
		/// Handles the <see cref="UnobservedTaskException"/> event in <see cref="System.Threading.Tasks"/> namespace.
		/// </summary>
		public static EventHandler<UnobservedTaskExceptionEventArgs> UnobservedTaskException
		{
			get
			{
                return UnobservedTaskExceptionHandler;
            }
		}

		/// <summary>
		/// Used for handling WPF exceptions bound to the UI thread.
		/// Handles the <see cref="Application.DispatcherUnhandledException"/> events in <see cref="System.Windows"/> namespace.
		/// </summary>
		/// <param name="sender">Exception sender object</param>
		/// <param name="e">Real exception is in: e.Exception</param>
		private static void DispatcherUnhandledExceptionHandler(object sender, DispatcherUnhandledExceptionEventArgs e)
		{
			if (Settings.HandleExceptions)
			{
				Logger.Trace("Starting to handle a System.Windows.Application.DispatcherUnhandledException.");
				new BugReport().Report(e.Exception, ExceptionThread.UI_WPF);
                e.Handled = true;
                Environment.Exit(0);
            }
		}

		/// <summary>
		/// Used for handling WinForms exceptions bound to the UI thread.
		/// Handles the <see cref="Application.ThreadException"/> events in <see cref="System.Windows.Forms"/> namespace.
		/// </summary>
		/// <param name="sender">Exception sender object.</param>
		/// <param name="e">Real exception is in: e.Exception</param>
		private static void ThreadExceptionHandler(object sender, ThreadExceptionEventArgs e)
		{
			if (Settings.HandleExceptions)
			{
				Logger.Trace("Starting to handle a System.Windows.Forms.Application.ThreadException.");

				// WinForms UI thread exceptions do not propagate to more general handlers unless: Application.SetUnhandledExceptionMode(UnhandledExceptionMode.ThrowException);
				new BugReport().Report(e.Exception, ExceptionThread.UI_WinForms);
                Environment.Exit(0);
            }
		}

		/// <summary>
		/// Used for handling general exceptions bound to the main thread.
		/// Handles the <see cref="AppDomain.UnhandledException"/> events in <see cref="System"/> namespace.
		/// </summary>
		/// <param name="sender">Exception sender object.</param>
		/// <param name="e">Real exception is in: ((Exception)e.ExceptionObject)</param>
		private static void UnhandledExceptionHandler(object sender, UnhandledExceptionEventArgs e)
		{
			if (Settings.HandleExceptions)
			{
				Logger.Trace("Starting to handle a System.AppDomain.UnhandledException.");
				new BugReport().Report((Exception)e.ExceptionObject, ExceptionThread.Main);
                Environment.Exit(0);
            }
		}

		/// <summary>
		/// Used for handling System.Threading.Tasks bound to a background worker thread.
		/// Handles the <see cref="UnobservedTaskException"/> event in <see cref="System.Threading.Tasks"/> namespace.
		/// </summary>
		/// <param name="sender">Exception sender object.</param>
		/// <param name="e">Real exception is in: e.Exception.</param>
		private static void UnobservedTaskExceptionHandler(object sender, UnobservedTaskExceptionEventArgs e)
		{
			if (Settings.HandleExceptions)
			{
				Logger.Trace("Starting to handle a System.Threading.Tasks.UnobservedTaskException.");
				new BugReport().Report(e.Exception, ExceptionThread.Task);
                e.SetObserved();
                Environment.Exit(0);
            }
		}

        // Vectored exception handler and minidump writer

        private static int VectoredHandler(IntPtr exceptionPointers)
        {
            try
            {
                // Attempt to write a minidump from native exception pointers
                WriteMiniDump(exceptionPointers);
            }
            catch (Exception ex)
            {
                // Never throw from a vectored exception handler
                Logger.Trace($"MiniDump write failed: {ex}");
            }

            // Let other handlers / the OS continue processing (EXCEPTION_CONTINUE_SEARCH)
            return 0;
        }

        private static void WriteMiniDump(IntPtr exceptionPointers)
        {
			var filename = $"nbug_cse_{DateTime.UtcNow.ToFileTime()}.dmp";
			var path = string.Empty;
            if (Settings.StoragePath == Enums.StoragePath.WindowsTemp)
            {
                var directoryPath = Path.Combine(new[] { Path.GetTempPath(), Settings.EntryAssembly.GetName().Name });

                if (Directory.Exists(directoryPath) == false)
                {
                    Directory.CreateDirectory(directoryPath);
                }

                path = Path.Combine(directoryPath, filename);
                Logger.Trace("Creating cse dumpfile to Windows temp path: " + path);
            }
            else if (Settings.StoragePath == Enums.StoragePath.CurrentDirectory)
            {
                path = Path.Combine(Settings.NBugDirectory, filename);
                Logger.Trace("Creating cse dumpfile to entry assembly directory path: " + path);
            }
            else if (Settings.StoragePath == Enums.StoragePath.IsolatedStorage)
            {
                path = filename;
                Logger.Trace("Creating cse dumpfile to isolated storage path: [Isolated Storage Directory]\\" + path);
            }
            else if (Settings.StoragePath == Enums.StoragePath.Custom)
            {
                var directoryPath = Path.GetFullPath(Settings.StoragePath); // In case this is a relative path

                if (Directory.Exists(directoryPath) == false)
                {
                    Directory.CreateDirectory(directoryPath);
                }

                path = Path.Combine(directoryPath, filename);
                Logger.Trace("Creating cse dumpfile to custom path: " + path);
            }

            try
            {
                using var fs = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None);
                var hFile = fs.SafeFileHandle.DangerousGetHandle();

                var info = new MINIDUMP_EXCEPTION_INFORMATION
                {
                    ThreadId = GetCurrentThreadId(),
                    ExceptionPointers = exceptionPointers,
                    ClientPointers = 1
                };

                var ok = MiniDumpWriteDump(GetCurrentProcess(), GetCurrentProcessId(), hFile, MINIDUMP_TYPE.MiniDumpWithFullMemory, ref info, IntPtr.Zero, IntPtr.Zero);
                Logger.Trace($"MiniDumpWriteDump returned: {ok}. Path: {path}");
            }
            catch (Exception ex)
            {
                Logger.Trace($"Failed to create minidump file '{path}': {ex}");
            }
        }

        #region Native interop

        private struct MINIDUMP_EXCEPTION_INFORMATION
        {
            public uint ThreadId;
            public IntPtr ExceptionPointers;
            public int ClientPointers;
        }

        [Flags]
        private enum MINIDUMP_TYPE : uint
        {
            MiniDumpNormal = 0x00000000,
            MiniDumpWithDataSegs = 0x00000001,
            MiniDumpWithFullMemory = 0x00000002,
            // other flags can be added if needed
        }

        [DllImport("dbghelp.dll", SetLastError = true)]
        private static extern bool MiniDumpWriteDump(IntPtr hProcess, uint processId, IntPtr hFile, MINIDUMP_TYPE dumpType, ref MINIDUMP_EXCEPTION_INFORMATION expParam, IntPtr userStreamParam, IntPtr callbackParam);

        [DllImport("kernel32.dll")]
        private static extern IntPtr AddVectoredExceptionHandler(uint first, VectoredExceptionHandlerDelegate handler);

        [DllImport("kernel32.dll")]
        private static extern uint RemoveVectoredExceptionHandler(IntPtr handle);

        [DllImport("kernel32.dll")]
        private static extern uint GetCurrentThreadId();

        [DllImport("kernel32.dll")]
        private static extern uint GetCurrentProcessId();

        [DllImport("kernel32.dll")]
        private static extern IntPtr GetCurrentProcess();

        #endregion
    }
}