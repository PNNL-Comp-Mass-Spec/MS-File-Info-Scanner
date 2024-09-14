using System;
using System.IO;
using PRISM;

namespace MSFileInfoScannerInterfaces
{
    /// <summary>
    /// Methods for reading and writing the processing status file
    /// </summary>
    public class StatusFileTools : EventNotifier
    {
        /// <summary>
        /// Read the status file
        /// </summary>
        /// <param name="statusFilePath">Status file path</param>
        /// <param name="status">Output: processing status</param>
        /// <returns>True if successful, false if an error</returns>
        public bool ReadStatusFile(string statusFilePath, out ProcessingStatus status)
        {
            status = new ProcessingStatus();

            try
            {
                // ToDo: Use an XML reader

                using var statusFileReader = new StreamReader(new FileStream(statusFilePath, FileMode.Create, FileAccess.Write, FileShare.Read));

                while (!statusFileReader.EndOfStream)
                {
                    statusFileReader.ReadLine();
                }

                return true;
            }
            catch (Exception ex)
            {
                OnErrorEvent(string.Format("Error writing the status to file {0}", statusFilePath ?? "?undefined?"), ex);
                return false;
            }
        }

        /// <summary>
        /// Update the status file
        /// </summary>
        /// <param name="statusFilePath">Status file path</param>
        /// <param name="status">Processing status</param>
        public void WriteStatusFile(string statusFilePath, ProcessingStatus status)
        {
            try
            {
                var tempPath = Path.Combine(AppUtils.GetAppDirectoryPath(), "Temp_" + Path.GetFileName(statusFilePath));

                using (var writer = new System.Xml.XmlTextWriter(tempPath, System.Text.Encoding.UTF8))
                {
                    writer.Formatting = System.Xml.Formatting.Indented;
                    writer.Indentation = 2;

                    writer.WriteStartDocument(true);
                    writer.WriteComment("MSFileInfoScanner processing status");

                    // Write the beginning of the "Root" element.
                    writer.WriteStartElement("Root");

                    writer.WriteStartElement("General");
                    writer.WriteElementString("LastUpdate", DateTime.Now.ToString("yyyy-MM-dd hh:mm:ss tt"));
                    writer.WriteElementString("Progress", StringUtilities.DblToString(status.ProgressPercentComplete, 2));
                    writer.WriteElementString("ProgressMessage", status.ProgressMessage);
                    writer.WriteElementString("ErrorCode", status.ErrorCode.ToString());
                    writer.WriteElementString("ErrorMessage", status.ErrorMessage);
                    writer.WriteEndElement();

                    writer.WriteEndElement();  // End the "Root" element.
                    writer.WriteEndDocument(); // End the document
                }

                // Copy the temporary file to the real one
                File.Copy(tempPath, statusFilePath, true);
                File.Delete(tempPath);
            }
            catch (Exception ex)
            {
                OnErrorEvent(string.Format("Error writing the status to file {0}", statusFilePath ?? "?undefined?"), ex);
            }
        }
    }
}
