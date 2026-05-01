// --------------------------------------------------------------------------------------------------------------------
// <copyright file="Settings.cs" company="NBug Project">
//   Copyright (c) 2011 - 2013 Teoman Soygul. Licensed under MIT license.
// </copyright>
// --------------------------------------------------------------------------------------------------------------------

namespace NBug
{
	using System;
	using System.Collections.Generic;
	using System.Collections.ObjectModel;
	using System.Configuration;
	using System.Diagnostics;
	using System.IO;
	using System.Linq;
	using System.Linq.Expressions;
	using System.Reflection;
	using System.Security.Cryptography;
	using System.Text;
	using System.Xml;
	using System.Xml.Linq;
	using System.Xml.XPath;

	using NBug.Core.Reporting;
	using NBug.Core.Reporting.Info;
	using NBug.Core.Submission;
	using NBug.Core.Util;
	using NBug.Core.Util.Exceptions;
	using NBug.Core.Util.Logging;
	using NBug.Enums;
	using NBug.Properties;

	using StoragePath = NBug.Core.Util.Storage.StoragePath;
    using NBug.Core.Util.Storage;

	public static class Settings
	{
		private static bool releaseMode; // False by default
		private static int sleepBeforeSend = 5; // Default: wait 5 seconds before sending queued reports

		static Settings()
		{
			// Crucial startup settings
			EntryAssembly = (Assembly.GetEntryAssembly() ?? Assembly.GetExecutingAssembly()) ?? Assembly.GetCallingAssembly();

				// GetEntryAssembly() is null if there is no initial GUI/CLI
			NBugDirectory = Path.GetDirectoryName(Assembly.GetCallingAssembly().Location) ?? Environment.CurrentDirectory;
			AdditionalReportFiles = new List<FileMask>();

            LoadAppconfigSettings();
        }

		/// <summary>
		/// The internal logger write event for getting notifications for all internal NBug loggers. Using this event, you can attach internal NBug
		/// logs to your applications own logging facility (i.e. log4net, NLog, etc.). First parameters is the message string, second one is the log
		/// category (info, warning, error, etc.).
		/// </summary>
		public static event Action<string, LoggerCategory> InternalLogWritten
		{
			add
			{
				Logger.LogWritten += value;
			}

			remove
			{
				Logger.LogWritten -= value;
			}
		}

		/// <summary>
		/// This event is fired just before any caught exception is processed, to make them into an orderly bug report. Parameters passed with
		/// this event can be inspected for some internal decision making or to add more information to the bug report. Supplied parameters are:
		/// -First parameter: <see cref="System.Exception"/>: This is the actual exception object that is caught to be report as a bug. This object
		/// is processed to extract standard information from it but it can still carry some custom data that you may want to use so it is supplied
		/// as a parameter of this event for your convenience.
		/// -Second parameter: <see cref="System.Object"/>: This is any XML serializable object which can carry any additional information to be
		/// embedded in the actual bug report. For instance you may capture  more information about the system than NBug does for you, so you can put
		/// all those new information in a user defined type and pass it here. You can also pass in any system type that is serializable. Make sure
		/// that passed objects are XML serializable or the information will not appear in the report. See the sample usage for proper usage if this
		/// event.
		/// </summary>
		/// <example>A sample code demonstrating the proper use of this event:
		/// <code>
		/// NBug.Settings.ProcessingException += (exception, report) =>
		///	{
		///		report.CustomInfo = new MyCusomSystemInformation { UtcTime = DateTime.UtcNow, AdditionalData = RubyExceptionData.GetInstance(exception) };
		///	};
		/// </code>
		/// </example>
		public static event Action<Exception, Report> ProcessingException
		{
			add
			{
				BugReport.ProcessingException += value;
			}

			remove
			{
				BugReport.ProcessingException -= value;
			}
		}

		/// <summary>
		/// Gets or sets a list of additional files to be added to the report zip. The files can use * or ? in the same way as DOS modifiers.
		/// </summary>
        public static List<FileMask> AdditionalReportFiles { get; set; }

		/// <summary>
		/// Gets or sets a value indicating whether the application will exit after handling and logging an unhandled exception.
		/// This value is disregarded for anything but UIMode.None. For UIMode.None, you can choose not to exit the application which will result in
		/// 'Windows Error Reporting' (aka Dr. Watson) window to kick in. One reason to do so would be to keep in line with Windows 7 Logo requirements,
		/// which is a corner case. This may also be helpful in using the NBug library as a simple unhandled exception logger facility, just to log and submit
		/// exceptions but not interfering with the application execution flow. Default value is true.
		/// </summary>
		public static bool ExitApplicationImmediately { get; set; }

		/// <summary>
		/// Gets or sets the memory dump type. Memory dumps are quite useful for replicating the exact conditions that the application crashed (i.e.
		/// getting the stack trace, local variables, etc.) but they take up a great deal of space, so choose wisely. Options are:
		/// None: No memory dump is generated.
		/// Tiny: Dump size ~200KB compressed.
		/// Normal: Dump size ~20MB compressed.
		/// Full: Dump size ~100MB compressed.
		/// Default value is Tiny.
		/// </summary>
		public static MiniDumpType MiniDumpType { get; set; }

		/// <summary>
		/// Gets or sets a value indicating whether to enable release mode for the NBug library. In release mode the internal developer UI is not displayed and
		/// unhandled exceptions are only handled if there is no debugger attached to the process. Once properly configured and verified to be working
		/// as intended, NBug release mode should be enabled to be able to properly use the Visual Studio debugger, without NBug trying to handle exceptions.
		/// before Visual Studio does. Default value is false.
		/// </summary>
		public static bool ReleaseMode
		{
			get
			{
				return releaseMode;
			}

			set
			{
				releaseMode = value;

				if (releaseMode)
				{
					ThrowExceptions = false;
					HandleExceptions = !Debugger.IsAttached;
				}
				else
				{
					// If developer mode is on (default)
					ThrowExceptions = true;
					HandleExceptions = true;
				}
			}
		}


		/// <summary>
		/// Gets or sets the bug report items storage path. After and unhandled exception occurs, the bug reports are created and queued for submission
		/// on the next application startup. Until then, the reports will be stored in this location. Default value is the application executable directory.
		/// This setting can either be assigned a full path string or a value from <see cref="NBug.Enums.StoragePath"/> enumeration.
		/// </summary>
		public static StoragePath StoragePath { get; set; }

		/// <summary>
		/// Gets or sets a value indicating whether to write "NLog.log" file to disk. Otherwise, you can subscribe to log events through the
		/// <see cref="InternalLogWritten"/> event. All the logging is done through System.Diagnostics.Trace.Write() function so you can also get
		/// the log with any trace listener. Default value is true.
		/// </summary>
		public static bool WriteLogToDisk { get; set; }

		/// <summary>
		/// Gets or sets the delay in seconds before attempting to send queued bug reports on application startup.
		/// Default value is 5 seconds.
		/// </summary>
		public static int SleepBeforeSend
		{
			get
			{
				return sleepBeforeSend;
			}

			set
			{
				sleepBeforeSend = value;
			}
		}

		/// <summary>
		/// Registers one or more bug report submission destinations and starts the dispatcher that processes
		/// any queued reports from previous runs. Call this once during application startup, after setting
		/// <see cref="ReleaseMode"/> and any other options.
		/// </summary>
		/// <param name="protocols">One or more protocol implementations to send reports to.</param>
		public static void StartSendingReportsInBackground(params IProtocol[] protocols)
		{
			if (protocols == null || protocols.Length == 0)
			{
				return;
			}

			_ = new Dispatcher(protocols);
		}

		/// <summary>
		/// Gets or sets the entry assembly which hosts the NBug assembly. It is used for retrieving the version and the full name
		/// of the host application. i.e. Settings.EntryAssembly.GetLoadedModules()[0].Name; @ Info\General.cs
		/// </summary>
		internal static Assembly EntryAssembly { get; set; }

		/// <summary>
		/// Gets or sets a value indicating whether the unhandled exception handlers in NBug.Handler class actually handle exceptions.
		/// Exceptions will not be handled if the application is in release mode via <see cref="Settings.ReleaseMode"/> and a debugger
		/// is attached to the process. This enables proper debugging of normal exceptions even in the presence of NBug.
		/// </summary>
		internal static bool HandleExceptions { get; set; }

		/// <summary>
		/// Gets or sets the absolute path to the directory that NBug.dll assembly currently resides. This is used in place of CWD
		/// throughout this assembly to prevent the library from getting affected of CWD changes that happens with Directory.SetCurrentDirectory().
		/// </summary>
		internal static string NBugDirectory { get; set; }

		/// <summary>
		/// Gets or sets a value indicating whether internal <see cref="NBugException"/> derived types are thrown or swallowed.
		/// Exceptions are NOT thrown by  default except for debug builds. Note that exceptions are caught and re-thrown by the
		/// Logger.Error() method with added information so stack trace is reset. The inner exceptions should be inspected to get
		/// the actual stack trace.
		/// </summary>
		internal static bool ThrowExceptions { get; set; }

		private static T GetDefaultEnumValue<T>()
		{
			var defaultSetting =
				typeof(Properties.Settings).GetProperty(typeof(T).Name).GetCustomAttributes(typeof(DefaultSettingValueAttribute), false)[0] as
				DefaultSettingValueAttribute;

			try
			{
				return (T)Enum.Parse(typeof(T), defaultSetting.Value);
			}
			catch (Exception exception)
			{
				throw new NBugRuntimeException(
					"There is no internal default value supplied for '" + typeof(T).Name + "' or the supplied value is invalid. See the inner exception for details.",
					exception);
			}
		}

		/// <summary>
		/// Replicate the behavior of normal Properties.Settings class via getting default values for null settings.
		/// Use this like GetDefaultValue(() =&gt; SleepBeforeSend);
		/// </summary>
		/// <returns>
		/// The <see cref="string"/>.
		/// </returns>
		private static string GetDefaultValue<T>(Expression<Func<T>> propertyExpression)
		{
			var defaultSetting =
				typeof(Properties.Settings).GetProperty(((MemberExpression)propertyExpression.Body).Member.Name)
				                           .GetCustomAttributes(typeof(DefaultSettingValueAttribute), false)[0] as DefaultSettingValueAttribute;
			return defaultSetting != null ? defaultSetting.Value : null;
		}

		private static string GetPropertyName<T>(Expression<Func<T>> propertyExpression)
		{
			return ((MemberExpression)propertyExpression.Body).Member.Name;
		}

		private static void LoadAppconfigSettings()
		{
			// Application settings
			StoragePath = Properties.Settings.Default.StoragePath;
			MiniDumpType = Properties.Settings.Default.MiniDumpType;
			WriteLogToDisk = Properties.Settings.Default.WriteLogToDisk;
			ExitApplicationImmediately = Properties.Settings.Default.ExitApplicationImmediately;
			ReleaseMode = Properties.Settings.Default.ReleaseMode;
		}
	}
}
