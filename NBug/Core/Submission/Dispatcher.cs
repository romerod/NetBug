// --------------------------------------------------------------------------------------------------------------------
// <copyright file="Dispatcher.cs" company="NBug Project">
//   Copyright (c) 2011 - 2013 Teoman Soygul. Licensed under MIT license.
// </copyright>
// --------------------------------------------------------------------------------------------------------------------

namespace NBug.Core.Submission
{
	using System;
	using System.Collections.Generic;
	using System.IO;
	using System.Threading;
	using System.Threading.Tasks;
	using System.Xml.Serialization;

	using NBug.Core.Reporting.Info;
	using NBug.Core.Util.Logging;
	using NBug.Core.Util.Serialization;
	using NBug.Core.Util.Storage;

	/// <summary>
	/// Dispatcher processes queued bug reports and submits them to configured destinations.
	/// </summary>
	internal class Dispatcher
	{
		private readonly IReadOnlyCollection<IProtocol> destinations;

		/// <summary>
		/// Initializes a new instance of the Dispatcher class to send queued reports asynchronously.
		/// </summary>
		/// <param name="destinations">The protocol destinations to send reports to.</param>
		internal Dispatcher(IReadOnlyCollection<IProtocol> destinations)
			: this(destinations, isAsynchronous: true)
		{
		}

		/// <summary>
		/// Initializes a new instance of the Dispatcher class to send queued reports.
		/// </summary>
		/// <param name="destinations">The protocol destinations to send reports to.</param>
		/// <param name="isAsynchronous">Decides whether to start the dispatching process asynchronously on a background thread.</param>
		internal Dispatcher(IReadOnlyCollection<IProtocol> destinations, bool isAsynchronous)
		{
			this.destinations = destinations;

			if (isAsynchronous)
			{
				// Log and swallow NBug's internal exceptions by default
				Task.Factory.StartNew(this.Dispatch)
					.ContinueWith(
						t => Logger.Error("An exception occurred while dispatching bug reports. Check the inner exception for details.", t.Exception),
						TaskContinuationOptions.OnlyOnFaulted);
			}
			else
			{
				try
				{
					this.Dispatch();
				}
				catch (Exception exception)
				{
					Logger.Error("An exception occurred while dispatching bug reports. Check the inner exception for details.", exception);
				}
			}
		}

		/// <summary>
		/// Dispatches all queued bug reports to configured destinations.
		/// </summary>
		private void Dispatch()
		{
			// Make sure that we are not interfering with the crucial startup work
			Thread.Sleep(Settings.SleepBeforeSend * 1000);

			// Now go through queued reports and submit to all configured destinations
			for (var hasReport = true; hasReport;)
			{
				using (var storer = new Storer())
				using (var stream = storer.GetFirstReportFile())
				{
					if (stream != null)
					{
						// Extract crash/exception report data from the zip file. 
						// Delete the zip file if no data can be retrieved (i.e. corrupt file)
						ExceptionData exceptionData;
						try
						{
							exceptionData = this.GetDataFromZip(stream);
						}
						catch (Exception exception)
						{
							storer.DeleteCurrentReportFile();
							Logger.Error("An exception occurred while extracting report data from zip file. Check the inner exception for details.", exception);
							return;
						}

						// Now submit the report file to all configured destinations
						if (!this.EnumerateDestinations(stream, exceptionData))
						{
							break;
						}

						// Delete the file after it was sent
						storer.DeleteCurrentReportFile();
					}
					else
					{
						hasReport = false;
					}
				}
			}
		}

		/// <summary>
		/// Enumerate all protocols to see if they are properly configured and send using the ones that are configured.
		/// </summary>
		/// <param name="reportFile">The file to read the report from.</param>
		/// <param name="exceptionData">The extracted exception data from the zip file.</param>
		/// <returns>Returns true if the report was submitted to at least one destination.</returns>
		private bool EnumerateDestinations(Stream reportFile, ExceptionData exceptionData)
		{
			var sentSuccessfullyAtLeastOnce = false;
			var fileName = Path.GetFileName(((FileStream)reportFile).Name);

			foreach (var destination in this.destinations)
			{
				try
				{
					Logger.Trace($"Submitting bug report via {destination.GetType().Name}.");
					reportFile.Position = 0; // Reset stream position for each destination
					if (destination.Send(fileName, reportFile, exceptionData.Report, exceptionData.Exception))
					{
						sentSuccessfullyAtLeastOnce = true;
						Logger.Trace($"Successfully submitted bug report via {destination.GetType().Name}.");
					}
					else
					{
						Logger.Warning($"Failed to submit bug report via {destination.GetType().Name}.");
					}
				}
				catch (Exception exception)
				{
					Logger.Error(
						$"An exception occurred while submitting bug report with {destination.GetType().Name}. Check the inner exception for details.",
						exception);
				}
			}

			return sentSuccessfullyAtLeastOnce;
		}

		/// <summary>
		/// Extracts exception and report data from the zip file.
		/// </summary>
		/// <param name="stream">The zip file stream.</param>
		/// <returns>An ExceptionData object containing the deserialized exception and report.</returns>
		private ExceptionData GetDataFromZip(Stream stream)
		{
			var results = new ExceptionData();
			var zipStorer = ZipStorer.Open(stream, FileAccess.Read);

			using (Stream zipItemStream = new MemoryStream())
			{
				var zipDirectory = zipStorer.ReadCentralDir();
				foreach (var entry in zipDirectory)
				{
					if (Path.GetFileName(entry.FilenameInZip) == StoredItemFile.Exception)
					{
						zipItemStream.SetLength(0);
						zipStorer.ExtractFile(entry, zipItemStream);
						zipItemStream.Position = 0;
						var deserializer = new XmlSerializer(typeof(SerializableException));
						results.Exception = (SerializableException)deserializer.Deserialize(zipItemStream);
						zipItemStream.Position = 0;
					}
					else if (Path.GetFileName(entry.FilenameInZip) == StoredItemFile.Report)
					{
						zipItemStream.SetLength(0);
						zipStorer.ExtractFile(entry, zipItemStream);
						zipItemStream.Position = 0;
						var deserializer = new XmlSerializer(typeof(Report));
						results.Report = (Report)deserializer.Deserialize(zipItemStream);
						zipItemStream.Position = 0;
					}
				}
			}

			return results;
		}

		/// <summary>
		/// Container for extracted exception data from a report zip file.
		/// </summary>
		private class ExceptionData
		{
			public SerializableException Exception { get; set; }

			public Report Report { get; set; }
		}
	}
}
