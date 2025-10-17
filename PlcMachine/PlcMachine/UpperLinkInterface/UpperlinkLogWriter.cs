using System;
using System.Collections.Concurrent;
using System.IO;
using System.Threading.Tasks;

namespace UpperLinkInterface
{
    public class UpperlinkLogWriter
    {
        public UpperlinkLogWriter(string portNumber)
        {
            m_plcName = $"SERIAL_{portNumber}";
            Task.Run(() => FileWrite());
        }

        private string m_plcName;

        private const string DirectoryName = "UpperlinkLog";
        private const string FileName = "UpperlinkLog.log";
        private const string BackupFileName = "BackupLog";
        private const uint LogFileSize = 8388608;	// 파일 크기 제한 - 8MByte
        private const uint LogKeepDate = 90;

        private ConcurrentQueue<string> m_logQueue = new ConcurrentQueue<string>();

        public void Log(string log)
        {
            string logInfo = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} {m_plcName}";
            string logMsg = $"{logInfo,-50}{log}";
            logMsg = logMsg.Replace("\r\n", $"\r\n{"",-50}");

            m_logQueue.Enqueue(logMsg);
        }

        public void LogError(Exception ex)
        {
            Log($"{ex.Message}\r\n{ex.StackTrace}");
        }

        private async Task FileWrite()
        {
            string directoryPath = Path.Combine(DirectoryName, m_plcName);
            string filePath = Path.Combine(directoryPath, FileName);
            while (true)
            {
                await Task.Delay(100);
                try
                {
                    if (m_logQueue.Count > 0)
                    {
                        if (!Directory.Exists(directoryPath))
                            Directory.CreateDirectory(directoryPath);

                        using (StreamWriter sw = new StreamWriter(filePath, true))
                            while (m_logQueue.TryDequeue(out var log))
                                sw.WriteLine(log);

                        FileInfo fileInfo = new FileInfo(filePath);
                        if (fileInfo.Length > LogFileSize)
                        {
                            BackupLogFile(fileInfo, BackupFileName);
                            DeleteOldLogFile(directoryPath, LogKeepDate, BackupFileName, ".log");
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex.Message);
                }
            }
        }

        private void BackupLogFile(FileInfo fileInfo, string backupFileName)
        {
            string backupFilePath = Path.Combine(fileInfo.DirectoryName, $"{backupFileName}_{DateTime.Now.ToString("yyyyMMddHHmmss")}.log");
            fileInfo.CopyTo(backupFilePath);
            fileInfo.Delete();

            //using (ZipArchive archive = ZipFile.Open($"{backupFilePath}.zip", ZipArchiveMode.Update))
            //{
            //    FileInfo backupFileInfo = new FileInfo(backupFilePath);
            //    archive.CreateEntryFromFile(backupFilePath, backupFileInfo.Name);
            //    backupFileInfo.Delete();
            //}
        }

        private void DeleteOldLogFile(string directoryPath, long logKeepDate, string filter, string extension)
        {
            DirectoryInfo di = new DirectoryInfo(directoryPath);
            DateTime criteraTime = DateTime.Now;
            foreach (var fileInfo in di.GetFiles())
            {
                if (fileInfo.Extension != extension || !fileInfo.Name.StartsWith(filter))
                    continue;

                DateTime creationTime = fileInfo.CreationTime;
                int passDate = (creationTime - creationTime).Days;
                if (passDate > logKeepDate)
                    fileInfo.Delete();
            }
        }
    }
}