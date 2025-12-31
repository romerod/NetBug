// --------------------------------------------------------------------------------------------------------------------
// <copyright file="BugReport.cs" company="NBug Project">
//   Copyright (c) 2011 - 2013 Teoman Soygul. Licensed under MIT license.
// </copyright>
// --------------------------------------------------------------------------------------------------------------------

namespace NBug.Core.Reporting
{
	using System;
	using System.IO;
	using System.Xml.Serialization;

	using NBug.Core.Reporting.Info;
	using NBug.Core.Reporting.MiniDump;
	using NBug.Core.Util;
	using NBug.Core.Util.Logging;
	using NBug.Core.Util.Serialization;
	using NBug.Core.Util.Storage;

	internal class BugReport
	{
		/// <summary>
		/// First parameters is the serializable exception object that is about to be processed, second parameter is any custom data
		/// object that the user wants to include in the report.
		/// </summary>
		internal static event Action<Exception, Report> ProcessingException;

		internal bool Report(Exception exception, ExceptionThread exceptionThread)
		{
			try
			{
				Logger.Trace("Starting to generate a bug report for the exception.");
				var serializableException = new SerializableException(exception);
				var report = new Report(serializableException);

				var handler = ProcessingException;
				if (handler != null)
				{
					Logger.Trace("Notifying the user before handling the exception.");

					// Allowing user to add any custom information to the report
					handler(exception, report);
				}

				this.CreateReportZip(serializableException, report);

				return true;
			}
			catch (Exception ex)
			{
				Logger.Error("An exception occurred during bug report generation process. See the inner exception for details.", ex);
				return false; // Since an internal exception occured
			}
		}

		// ToDo: PRIORITY TASK! This code needs more testing & condensation
		private void AddAdditionalFiles(ZipStorer zipStorer)
		{
			foreach (FileMask additionalFiles in Settings.AdditionalReportFiles)
			{
                additionalFiles.AddToZip(zipStorer);				
			}
		}


		private void CreateReportZip(SerializableException serializableException, Report report)
		{
            var reportFileName = "Exception_" + DateTime.UtcNow.ToFileTime() + ".zip";
            var minidumpFilePath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "Exception_MiniDump_" + DateTime.UtcNow.ToFileTime() + ".mdmp");

            using (var storer = new Storer())
            using (var zipStorer = ZipStorer.Create(storer.CreateReportFile(reportFileName), string.Empty))
            using (var stream = new MemoryStream())
            {
                // Store the exception
                var serializer = new XmlSerializer(typeof(SerializableException));
                serializer.Serialize(stream, serializableException);
                stream.Position = 0;
                zipStorer.AddStream(ZipStorer.Compression.Deflate, StoredItemFile.Exception, stream, DateTime.UtcNow, string.Empty);

                // Store the report
                stream.SetLength(0);

                try
                {
                    serializer = report.CustomInfo != null
                                     ? new XmlSerializer(typeof(Report), new[] { report.CustomInfo.GetType() })
                                     : new XmlSerializer(typeof(Report));

                    serializer.Serialize(stream, report);
                }
                catch (Exception exception)
                {
                    Logger.Error(
                        string.Format(
                            "The given custom info of type [{0}] cannot be serialized. Make sure that given type and inner types are XML serializable.",
                            report.CustomInfo.GetType()),
                        exception);
                    report.CustomInfo = null;
                    serializer = new XmlSerializer(typeof(Report));
                    serializer.Serialize(stream, report);
                }

                stream.Position = 0;
                zipStorer.AddStream(ZipStorer.Compression.Deflate, StoredItemFile.Report, stream, DateTime.UtcNow, string.Empty);

                // Add the memory minidump to the report file (only if configured so)
                if (DumpWriter.Write(minidumpFilePath))
                {
                    zipStorer.AddFile(ZipStorer.Compression.Deflate, minidumpFilePath, StoredItemFile.MiniDump, string.Empty);
                    File.Delete(minidumpFilePath);
                }

                // Add any user supplied files in the report (if any)
                if (Settings.AdditionalReportFiles.Count != 0)
                {
                    // ToDo: This needs a lot more work!
                    this.AddAdditionalFiles(zipStorer);
                }
            }

            Logger.Trace("Created a new report file. Currently the number of report files queued to be send is: " + Storer.GetReportCount());
        }
	}
}