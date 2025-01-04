
namespace AchopocalypseCli
{
    public class FileLogger : IDisposable
    {
        private FileStream _logFile;
        private StreamWriter _writer;

        public FileLogger(string filepath)
        {
            _logFile = File.OpenWrite(filepath);
            _writer = new StreamWriter(_logFile);
        }

        public void WriteLine(string line)
        {
            _writer.WriteLine($"{DateTime.Now} - {line}");
            _writer.Flush();
        }

        public void WriteException(Exception exception)
        {
            _writer.WriteLine($"{DateTime.Now} - {exception.ToString()}");
            _writer.Flush();
        }

        public void Dispose()
        {
            _writer.Dispose();
            _logFile.Dispose();
        }
    }
}
