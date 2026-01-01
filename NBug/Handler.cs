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
				new BugReport().Report(e.Exception);
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
				new BugReport().Report(e.Exception);
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
				new BugReport().Report((Exception)e.ExceptionObject);
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
				new BugReport().Report(e.Exception);
                e.SetObserved();
                Environment.Exit(0);
            }
		}
    }
}