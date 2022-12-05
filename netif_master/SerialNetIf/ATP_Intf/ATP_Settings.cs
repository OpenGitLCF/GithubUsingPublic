using System;
using System.Collections.Generic;
using System.Text;
using System.Runtime.InteropServices;

namespace SerialNetIf
{
    class ATP_Settings
    {
        private static string iniFilePath;
        public static string terminateExecEventName;
        public static string reportFolderPath = String.Empty;

        private static string atpIniKey = "ATP";
        private static string reportPathIniValue = "Report folder path";
        private static string terminateEventIniValue = "Terminate execution event name";
        private static string defaultTerminateEventName = "ATP Terminate Execution Event";

        public const string TIME_FORMAT = "HH:mm:ss:fff";

        public static void LoadSettingsFromFile(string filePath)
        {
            iniFilePath = filePath;

            try
            {
                reportFolderPath = IniReadValue(atpIniKey, reportPathIniValue);
                terminateExecEventName = IniReadValue(atpIniKey, terminateEventIniValue);
                if (String.IsNullOrEmpty(terminateEventIniValue))
                {
                    terminateExecEventName = defaultTerminateEventName;
                }
                //add Pid after string read from file for multi-instance ATP;
                terminateExecEventName += " " + System.Diagnostics.Process.GetCurrentProcess().Id;
            }
            catch (Exception ex)
            {
                SerialNetIf.logger.WriteLine(ex.ToString());
            }
        }



        [DllImport("kernel32")]
        private static extern int GetPrivateProfileString(string section,
                 string key, string def, StringBuilder retVal,
            int size, string filePath);

        [DllImport("KERNEL32.DLL", EntryPoint = "GetPrivateProfileStringW",
          SetLastError = true,
          CharSet = CharSet.Unicode, ExactSpelling = true,
          CallingConvention = CallingConvention.StdCall)]
        private static extern int GetPrivateProfileString(
            string lpAppName,
            string lpKeyName,
            string lpDefault,
            string lpReturnString,
            int nSize,
            string lpFilename);



        private const int keyBufferSize = 256;


        private static string IniReadValue(string Section, string Key)
        {
            StringBuilder temp = new StringBuilder(keyBufferSize);
            GetPrivateProfileString(Section, Key, "", temp,
                                            keyBufferSize, iniFilePath);
            return temp.ToString();
        }
    }



    // Multiuse attribute.
    [System.AttributeUsage(System.AttributeTargets.Method,
                           AllowMultiple = true)  // Multiuse attribute.
    ]
    public class Parameter : System.Attribute
    {
        string name;
        string type;
        string defaultValue;

        public Parameter(string name, string type)
        {
            this.name = name;
            this.type = type;
            defaultValue = String.Empty;
        }

        public Parameter(string name, string type, string defaultValue)
            : this(name, type)
        {
            this.defaultValue = defaultValue;
        }

        public string GetName()
        {
            return name;
        }

        public string GetParameterType()
        {
            return type;
        }

        public string GetDefaultValue()
        {
            return defaultValue;
        }
    }


    public enum TestStatus
    {
        NOT_EXECUTED,
        RUNNING,
        PASSED,
        FAILED,
        BLOCKED
    }

}
