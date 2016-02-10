using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;

namespace iRacingAmbience
{
    public class FileLogger
    {
        private static FileLogger singleton;
        private StreamWriter logWriter;

        public static FileLogger Instance
        {
            get { return singleton ?? (singleton = new FileLogger()); }
        }

        public void Open(string filePath, bool append)
        {
            if (this.logWriter != null)
                throw new InvalidOperationException(
                    "Logger is already open");

            this.logWriter = new StreamWriter(filePath, append);
            this.logWriter.AutoFlush = true;
        }

        public void Close()
        {
            if (this.logWriter != null)
            {
                this.logWriter.Close();
                this.logWriter.Dispose();
                this.logWriter = null;
            }
        }

        public void Write(string entry)
        {
            if (this.logWriter == null)
                throw new InvalidOperationException(
                    "Logger is not open");
            this.logWriter.WriteLine("{0}",

                 entry);
        }
    }
}
