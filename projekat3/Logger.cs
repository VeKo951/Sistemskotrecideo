using System;
using System.IO;

namespace projekat3
{
    internal class Logger
    {
        private readonly object locker = new object();
        private readonly string logFile = "log.txt";

        public void Log(string message)
        {
            lock (locker)
            {
                string text = $"[{DateTime.Now:HH:mm:ss}] {message}";

                Console.WriteLine(text);
                File.AppendAllText(logFile, text + Environment.NewLine);
            }
        }
    }
}