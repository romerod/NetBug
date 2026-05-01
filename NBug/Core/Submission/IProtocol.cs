// --------------------------------------------------------------------------------------------------------------------
// <copyright file="IProtocol.cs" company="NBug Project">
//   Copyright (c) 2011 - 2013 Teoman Soygul. Licensed under MIT license.
// </copyright>
// --------------------------------------------------------------------------------------------------------------------

namespace NBug.Core.Submission
{
	using System.IO;
	using NBug.Core.Reporting.Info;
	using NBug.Core.Util.Serialization;

	/// <summary>
	/// Interface for bug report submission protocols.
	/// </summary>
	public interface IProtocol
	{
		/// <summary>
		/// Sends a bug report using the specific protocol implementation.
		/// </summary>
		/// <param name="fileName">Name of the report file.</param>
		/// <param name="file">Stream containing the report zip file.</param>
		/// <param name="report">The bug report information.</param>
		/// <param name="exception">The serialized exception.</param>
		/// <returns>True if the report was sent successfully; otherwise, false.</returns>
		bool Send(string fileName, Stream file, Report report, SerializableException exception);
	}
}
