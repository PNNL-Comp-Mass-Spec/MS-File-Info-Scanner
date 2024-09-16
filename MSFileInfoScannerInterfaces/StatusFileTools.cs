using System;
using System.IO;
using System.Xml;
using PRISM;

namespace MSFileInfoScannerInterfaces
{
    /// <summary>
    /// Methods for reading and writing the processing status file
    /// </summary>
    public class StatusFileTools : EventNotifier
    {
        private DateTime GetXmlValue(XmlNode node, DateTime valueIfMissing)
        {
            if (node == null)
                return valueIfMissing;

            return DateTime.TryParse(node.InnerText, out var value) ? value : valueIfMissing;
        }

        private float GetXmlValue(XmlNode node, float valueIfMissing)
        {
            if (node == null)
                return valueIfMissing;

            return float.TryParse(node.InnerText, out var value) ? value : valueIfMissing;
        }

        private iMSFileInfoScanner.MSFileScannerErrorCodes GetXmlValue(XmlNode node, iMSFileInfoScanner.MSFileScannerErrorCodes valueIfMissing)
        {
            if (node == null)
                return valueIfMissing;

            return Enum.TryParse(node.InnerText, out iMSFileInfoScanner.MSFileScannerErrorCodes value) ? value : valueIfMissing;
        }

        private string GetXmlValue(XmlNode node, string valueIfMissing)
        {
            return node == null ? valueIfMissing : node.InnerText;
        }

        // ReSharper disable once UnusedMember.Global

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
                var statusFile = new FileInfo(statusFilePath);
                if (!statusFile.Exists)
                {
                    OnErrorEvent("Status file not found: {0}", statusFile.FullName);
                    return false;
                }

                using var statusFileReader = new FileStream(statusFile.FullName, FileMode.Open, FileAccess.Read, FileShare.Read);

                // Open the file and parse the XML
                var doc = new XmlDocument();
                doc.Load(statusFileReader);

                // Look for the XML: <General></General>
                var generalNode = doc.SelectSingleNode("//Root/General");

                // ReSharper disable once MergeIntoNegatedPattern
                if (generalNode == null || !generalNode.HasChildNodes)
                {
                    OnWarningEvent("The <Root><General> node does not have any child nodes");
                    return false;
                }

                status.LastUpdate = GetXmlValue(generalNode.SelectSingleNode("LastUpdate"), DateTime.MinValue);
                status.ProgressPercentComplete = GetXmlValue(generalNode.SelectSingleNode("Progress"), 0.0f);
                status.ProgressMessage = GetXmlValue(generalNode.SelectSingleNode("ProgressMessage"), string.Empty);
                status.ErrorCode = GetXmlValue(generalNode.SelectSingleNode("ErrorCode"), iMSFileInfoScanner.MSFileScannerErrorCodes.NoError);
                status.ErrorMessage = GetXmlValue(generalNode.SelectSingleNode("ErrorMessage"), string.Empty);

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

                using (var writer = new XmlTextWriter(tempPath, System.Text.Encoding.UTF8))
                {
                    writer.Formatting = Formatting.Indented;
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
                    writer.WriteElementString("ErrorCountLoadDataForScan", status.ErrorCountLoadDataForScan.ToString());
                    writer.WriteElementString("ErrorCountUnknownScanFilterFormat", status.ErrorCountUnknownScanFilterFormat.ToString());
                    writer.WriteEndElement();   // End the "General" element

                    writer.WriteEndElement();   // End the "Root" element
                    writer.WriteEndDocument();  // End the document
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
