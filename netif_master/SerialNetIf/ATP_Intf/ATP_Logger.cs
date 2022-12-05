using System;
using System.Collections.Generic;
using System.Text;
using System.IO;

namespace SerialNetIf
{
    public class ATP_Logger
    {
        private StreamWriter logFileWriter = null;
        private FileStream logFileStream = null;
        private string logFilePath;
        static readonly object padlock = new object();

        private static int instancesNo = 0;
        private static ATP_Logger thisInstance = null;

        private ATP_Logger(string logFilePathParam)
        {
            try
            {
                logFilePath = logFilePathParam;

                lock (padlock)
                {
                    logFileStream = File.Open(logFilePath, FileMode.Append, FileAccess.Write, FileShare.ReadWrite);
                    logFileWriter = new StreamWriter(logFileStream);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.ToString());
            }
        }

        public static ATP_Logger GetInstance(string logFilePathParam)
        {
            instancesNo++;

            if (null == thisInstance)
            {
                thisInstance = new ATP_Logger(logFilePathParam);
            }

            return thisInstance;
        }

        public void LogVar(string name, List<string> expectedParams)
        {
            try
            {
                lock (padlock)
                {
                    if (null != logFileWriter && null != logFileWriter.BaseStream)
                    {
                        logFileWriter.Write(TimeStamp() + " -> ");
                        StringBuilder sb = new StringBuilder();
                        sb.AppendFormat("{0}:\r\n", name);
                        foreach (var kv in expectedParams)
                        {
                            sb.AppendFormat("\t{0},\r\n", kv);
                        }
                        logFileWriter.WriteLine(sb.ToString());
                        logFileWriter.Flush();
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.ToString());
            }

        }

        public void LogVar(string name,Dictionary<string, string> parameters)
        {
            try
            {
                lock (padlock)
                {
                    if (null != logFileWriter && null != logFileWriter.BaseStream)
                    {
                        logFileWriter.Write(TimeStamp() + " -> ");
                        StringBuilder sb = new StringBuilder();
                        sb.AppendFormat("{0}:\r\n", name);
                        foreach(var kv in parameters)
                        {
                            sb.AppendFormat("\t{0} = {1}\r\n", kv.Key, kv.Value);
                        }
                        logFileWriter.WriteLine(sb.ToString());
                        logFileWriter.Flush();
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.ToString());
            }

        }

        public void WriteLine(string message)
        {
            try
            {
                lock (padlock)
                {
                    if (null != logFileWriter && null != logFileWriter.BaseStream)
                    {
                        logFileWriter.Write(TimeStamp() + " -> ");
                        logFileWriter.WriteLine(message);
                        logFileWriter.Flush();
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.ToString());
            }

        }

        private string TimeStamp()
        {
            DateTime now = DateTime.Now;
            return now.ToString(ATP_Settings.TIME_FORMAT); ;
        }

        public void CloseLogger()
        {
            try
            {
                lock (padlock)
                {
                    instancesNo--;

                    WriteLine("Close logger: instances no = " + instancesNo);
                    if (instancesNo == 0)
                    {
                        logFileWriter.Close();
                        logFileStream.Close();
                        thisInstance = null;
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.ToString());
            }
        }
    }
}
