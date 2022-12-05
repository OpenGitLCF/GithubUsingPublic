using System;
using System.Collections.Generic;
using System.Text;
using System.Reflection;
using System.Threading;

// In order to use this template, after changing it's name, please make sure that 
// the DLL FILE, the NAMESPACE and the CLASS contained in the dll have the same name.
// (Functions shall be extracted only from the class that is named the same as the namespace and the file)

namespace SerialNetIf
{
    public class SerialNetIf
    {
        #region Declarations
        // Index names
        private const byte VERDICT = 0;
        private const byte REASON = 1;

        private const string logFileName = "Debug\\SerialNetIf.txt";    // TODO: Update with dll name
        private const string configFileName = "ATP_Config.ini";
        private const string DLLS_FOLDER_NAME = "FunctionDlls";

        public static ATP_Logger logger = null;

        private EventWaitHandle terminateEvent;

        NetIfDevEx m_Dev = new NetIfDevEx();
        #endregion


        #region Constructor / Destructor

        public SerialNetIf()
        {
            try
            {
                ATP_Settings.LoadSettingsFromFile(AppDomain.CurrentDomain.BaseDirectory + "\\" + configFileName);
                terminateEvent = EventWaitHandle.OpenExisting(ATP_Settings.terminateExecEventName);

                logger = ATP_Logger.GetInstance(ATP_Settings.reportFolderPath + "\\" + logFileName);
            }
            catch (Exception ex)
            {
                Console.WriteLine("Constructor: " + ex.ToString());
            }
        }

        // Will be called when finished using the instance of this class
        public void Clean()
        {
            try
            {
                if (null != logger)
                {
                    logger.CloseLogger();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Dispose: " + ex.ToString());
            }
        }

        #endregion



        #region General Functions - available to ATP
        [Parameter("Port", "string", "COM1")] // or
        [Parameter("Baudrate", "int", "115200")]
        public string[] NetIfOpen(Dictionary<string, string> parameters,
            List<string> expectedParams, EventWaitHandle eventLaunched)
        {
            logger.LogVar("SerialNetIfOpen parameters", parameters);
            logger.LogVar("SerialNetIfOpen expectedParams", expectedParams);

            string[] result = { TestStatus.PASSED.ToString(), "" };

            try
            {
                // Please set the eventLaunched when the function is considered to have "launched" 
                // (but as soon as possible)
                if (null != eventLaunched)
                {
                    eventLaunched.Set();
                }

                int baud = int.Parse(parameters["Baudrate"]);
                if (!m_Dev.Open(parameters["Port"], baud))
                {
                    result[VERDICT] = TestStatus.FAILED.ToString();
                    result[REASON] = parameters["Port"] + "Open Failed!";
                }
                else
                {

                }
            }
            catch (Exception ex)
            {
                logger.WriteLine(ex.ToString());
                result[VERDICT] = TestStatus.FAILED.ToString();
                result[REASON] = ex.Message;
            }
            return result;
        }

        public string[] NetIfClose(Dictionary<string, string> parameters,
           List<string> expectedParams, EventWaitHandle eventLaunched)
        {
            string[] result = { TestStatus.PASSED.ToString(), "" };

            try
            {
                // Please set the eventLaunched when the function is considered to have "launched" 
                // (but as soon as possible)
                if (null != eventLaunched)
                {
                    eventLaunched.Set();
                }
                m_Dev.Close();
            }
            catch (Exception ex)
            {
                logger.WriteLine(ex.ToString());
                result[VERDICT] = TestStatus.FAILED.ToString();
                result[REASON] = ex.Message;
            }
            return result;
        }

        [Parameter("MsgID", "string", "")]
        public string[] CleanReceivedMessage(Dictionary<string, string> parameters,
           List<string> expectedParams, EventWaitHandle eventLaunched)
        {
            string[] result = { TestStatus.PASSED.ToString(), "Success" };

            try
            {
                // Please set the eventLaunched when the function is considered to have "launched" 
                // (but as soon as possible)
                if (null != eventLaunched)
                {
                    eventLaunched.Set();
                }
                // Check Parameters
                uint id = 0x1FFFFFFF;
                if (!string.IsNullOrEmpty(parameters["MsgID"]))
                {
                    id = Convert.ToUInt32(parameters["MsgID"], 16);

                    if (id >= 0x20000000)
                    {
                        result[VERDICT] = TestStatus.FAILED.ToString();
                        result[REASON] = " Parameter CmdID Error!";
                        return result;
                    }
                }
                
                if (!m_Dev.CleanReceivedMessage(id))
                {
                    result[VERDICT] = TestStatus.FAILED.ToString();
                    result[REASON] = " CleanReceivedMessage Failed!";
                }
            }
            catch (Exception ex)
            {
                logger.WriteLine(ex.ToString());
                result[VERDICT] = TestStatus.FAILED.ToString();
                result[REASON] = ex.Message;
            }
            return result;
        }

        [Parameter("CmdID", "string", "0x800")] 
        [Parameter("CmdData", "multistring", "")]
        [Parameter("ResponseTimeout", "double", "0.1")]
        public string[] SendCommand_WaitResponse(Dictionary<string, string> parameters,
            List<string> expectedParams, EventWaitHandle eventLaunched)
        {
            logger.LogVar("SendCmd_WaitResp parameters", parameters);
            logger.LogVar("SendCmd_WaitResp expectedParams", expectedParams);

            string[] result = { TestStatus.PASSED.ToString(), "" };

            try
            {
                // Please set the eventLaunched when the function is considered to have "launched" 
                // (but as soon as possible)
                if (null != eventLaunched)
                {
                    eventLaunched.Set();
                }

                if (!CheckExecCondition(result))
                    return result;
                // Check Parameters
                uint id = Convert.ToUInt32(parameters["CmdID"], 16);
                if (id >= 0x20000000)
                {
                    result[VERDICT] = TestStatus.FAILED.ToString();
                    result[REASON] = " Parameter CmdID Error!";
                    return result;
                }
                byte[] data = null;
                if(!string.IsNullOrEmpty(parameters["CmdData"].Trim()))
                {
                    try
                    {
                        data = StringUtil.HexStringToByteArray(parameters["CmdData"]);
                    }
                    catch
                    {
                        result[VERDICT] = TestStatus.FAILED.ToString();
                        result[REASON] = " Parameter CmdData Error!";
                        return result;
                    }
                }


                double waitmo = double.Parse(parameters["ResponseTimeout"]);
                byte[] response = null;
                if (!m_Dev.SendCmdWaitResponse(id, data, waitmo, out response))
                {
                    result[VERDICT] = TestStatus.FAILED.ToString();
                    result[REASON] = " SendCmd_WaitResp Failed!";
                }
                else
                {
                    if (null == response)
                        result[REASON] = "";
                    else
                    {
                        string s = StringUtil.ByteArrayToHexString(response);
                        result[REASON] = s;
                    }
                }
            }
            catch (Exception ex)
            {
                logger.WriteLine(ex.ToString());
                result[VERDICT] = TestStatus.FAILED.ToString();
                result[REASON] = ex.Message;
            }
            return result;
        }
        [Parameter("CmdID", "string", "0x800")] // or
        [Parameter("CmdData", "multistring", "1122334455667788")]
        public string[] SendCommand(Dictionary<string, string> parameters,
            List<string> expectedParams, EventWaitHandle eventLaunched)
        {
            logger.LogVar("SendCommand parameters", parameters);
            logger.LogVar("SendCommand expectedParams", expectedParams);

            string[] result = { TestStatus.PASSED.ToString(), "" };

            try
            {
                // Please set the eventLaunched when the function is considered to have "launched" 
                // (but as soon as possible)
                if (null != eventLaunched)
                {
                    eventLaunched.Set();
                }

                if (!CheckExecCondition(result))
                    return result;
                // Check Parameters
                uint id = Convert.ToUInt32(parameters["CmdID"], 16);
                if (id >= 0x20000000)
                {
                    result[VERDICT] = TestStatus.FAILED.ToString();
                    result[REASON] = " Parameter CmdID Error!";
                    return result;
                }
                byte[] data = null;
                if (!string.IsNullOrEmpty(parameters["CmdData"].Trim()))
                {
                    try
                    {
                        data = StringUtil.HexStringToByteArray(parameters["CmdData"]);
                    }
                    catch
                    {
                        result[VERDICT] = TestStatus.FAILED.ToString();
                        result[REASON] = " Parameter CmdData Error!";
                        return result;
                    }
                }

                if (!m_Dev.SendMessage(id, data))
                {
                    result[VERDICT] = TestStatus.FAILED.ToString();
                    result[REASON] = " SendCmd_WaitResp Failed!";
                }
            }
            catch (Exception ex)
            {
                logger.WriteLine(ex.ToString());
                result[VERDICT] = TestStatus.FAILED.ToString();
                result[REASON] = ex.Message;
            }
            return result;
        }

        [Parameter("RespID", "string", "0x800")]
        public string[] ReadResponse(Dictionary<string, string> parameters,
            List<string> expectedParams, EventWaitHandle eventLaunched)
        {
            logger.LogVar("ReadResponse parameters", parameters);
            logger.LogVar("ReadResponse expectedParams", expectedParams);

            string[] result = { TestStatus.PASSED.ToString(), "" };

            try
            {
                // Please set the eventLaunched when the function is considered to have "launched" 
                // (but as soon as possible)
                if (null != eventLaunched)
                {
                    eventLaunched.Set();
                }

                if (!CheckExecCondition(result))
                    return result;
                // Check Parameters
                uint id = Convert.ToUInt32(parameters["RespID"], 16);
                if (id >= 0x20000000)
                {
                    result[VERDICT] = TestStatus.FAILED.ToString();
                    result[REASON] = " Parameter CmdID Error!";
                    return result;
                }
                byte[] response = null;
                if (!m_Dev.ReadResponse(id, out response))
                {
                    result[VERDICT] = TestStatus.FAILED.ToString();
                    result[REASON] = " ReadResponse Failed!";
                }
                else
                {
                    if (null == response)
                        result[REASON] = "";
                    else
                        result[REASON] = StringUtil.ByteArrayToHexString(response);
                }
            }
            catch (Exception ex)
            {
                logger.WriteLine(ex.ToString());
                result[VERDICT] = TestStatus.FAILED.ToString();
                result[REASON] = ex.Message;
            }
            return result;
        }


        [Parameter("NotifyID", "string", "0x8000")]
        [Parameter("WaitTimeout", "double", "0.1")]
        public string[] WaitNotify(Dictionary<string, string> parameters,
            List<string> expectedParams, EventWaitHandle eventLaunched)
        {
            string[] result = { TestStatus.PASSED.ToString(), "" };

            try
            {
                // Please set the eventLaunched when the function is considered to have "launched" 
                // (but as soon as possible)
                if (null != eventLaunched)
                {
                    eventLaunched.Set();
                }

                if (!CheckExecCondition(result))
                    return result;

                uint id = Convert.ToUInt32(parameters["NotifyID"], 16);
                double waitmo = double.Parse(parameters["WaitTimeout"]);
                byte[] response = null;
                if (!m_Dev.WaitNotify(id, waitmo, out response))
                {
                    result[VERDICT] = TestStatus.FAILED.ToString();
                    result[REASON] = " WaitNotify Failed!";
                }
                else
                {
                    if (null == response)
                        result[REASON] = "";
                    else
                        result[REASON] = StringUtil.ByteArrayToHexString(response);
                }
            }
            catch (Exception ex)
            {
                logger.WriteLine(ex.ToString());
                result[VERDICT] = TestStatus.FAILED.ToString();
                result[REASON] = ex.Message;
            }
            return result;
        }
        #endregion

        #region ATP DUT Functions - available to ATP
        [Parameter("DutID", "int", "0")]
        public string[] DutCheckConnect(Dictionary<string, string> parameters,
            List<string> expectedParams, EventWaitHandle eventLaunched)
        {
            string[] result = { TestStatus.PASSED.ToString(), "" };

            try
            {
                // Please set the eventLaunched when the function is considered to have "launched" 
                // (but as soon as possible)
                if (null != eventLaunched)
                {
                    eventLaunched.Set();
                }
                int id = Convert.ToInt32(parameters["DutID"], 16);
                if (!m_Dev.DutCheckConnect(id))
                {
                    result[VERDICT] = TestStatus.FAILED.ToString();
                    result[REASON] = " Dut Connect Failed!";
                }
                else
                {
                    result[REASON] = " Dut Connect Passed!";
                }
            }
            catch (Exception ex)
            {
                logger.WriteLine(ex.ToString());
                result[VERDICT] = TestStatus.FAILED.ToString();
                result[REASON] = ex.Message;
            }
            return result;
        }

        [Parameter("DutID", "int", "0")]
        public string[] DutCheckReset(Dictionary<string, string> parameters,
           List<string> expectedParams, EventWaitHandle eventLaunched)
        {
            string[] result = { TestStatus.PASSED.ToString(), "" };

            try
            {
                // Please set the eventLaunched when the function is considered to have "launched" 
                // (but as soon as possible)
                if (null != eventLaunched)
                {
                    eventLaunched.Set();
                }
                int id = Convert.ToInt32(parameters["DutID"], 16);
                if (!m_Dev.DutCheckReset(id))
                {
                    result[VERDICT] = TestStatus.FAILED.ToString();
                    result[REASON] = " Dut has Reset!";
                }
                else
                {
                    result[REASON] = " Dut don't Reset!";
                }
            }
            catch (Exception ex)
            {
                logger.WriteLine(ex.ToString());
                result[VERDICT] = TestStatus.FAILED.ToString();
                result[REASON] = ex.Message;
            }
            return result;
        }
        #endregion

        #region ATP Device Functions - available to ATP
        [Parameter("Channel", "string", "LIN0,LIN1,LIN2,LIN3")]
        [Parameter("Baudrate", "int", "19200")]
        [Parameter("Master", "bool", "false")]
        public string[] DevLinChanConfig(Dictionary<string, string> parameters,
            List<string> expectedParams, EventWaitHandle eventLaunched)
        {
            string[] result = { TestStatus.PASSED.ToString(), "" };

            try
            {
                // Please set the eventLaunched when the function is considered to have "launched" 
                // (but as soon as possible)
                if (null != eventLaunched)
                {
                    eventLaunched.Set();
                }
                m_Dev.DevLinChanConfig(parameters["Channel"], parameters["Baudrate"], parameters["Master"]);
            }
            catch (Exception ex)
            {
                logger.WriteLine(ex.ToString());
                result[VERDICT] = TestStatus.FAILED.ToString();
                result[REASON] = ex.Message;
            }
            return result;
        }

        [Parameter("Channel", "string", "LIN0,LIN1,LIN2,LIN3")]
        [Parameter("RequireID", "string", "0x05")]
        [Parameter("HeaderFrame", "bool", "false")]
        [Parameter("RequireData", "string", "1122334455667788")]
        public string[] DevLinSendMasterRequire(Dictionary<string, string> parameters,
            List<string> expectedParams, EventWaitHandle eventLaunched)
        {
            string[] result = { TestStatus.PASSED.ToString(), "" };

            try
            {
                // Please set the eventLaunched when the function is considered to have "launched" 
                // (but as soon as possible)
                if (null != eventLaunched)
                {
                    eventLaunched.Set();
                }
                m_Dev.DevLinSendMasterRequire(parameters["Channel"], parameters["RequireID"], parameters["Response Data"]);
            }
            catch (Exception ex)
            {
                logger.WriteLine(ex.ToString());
                result[VERDICT] = TestStatus.FAILED.ToString();
                result[REASON] = ex.Message;
            }
            return result;
        }

        [Parameter("Channel", "string", "LIN0,LIN1,LIN2,LIN3")]
        [Parameter("ResponseID", "string", "0x05")] // or
        [Parameter("ResponseData", "string", "1122334455667788")]
        public string[] DevLinUpdateSlaveResponse(Dictionary<string, string> parameters,
            List<string> expectedParams, EventWaitHandle eventLaunched)
        {
            string[] result = { TestStatus.PASSED.ToString(), "" };

            try
            {
                // Please set the eventLaunched when the function is considered to have "launched" 
                // (but as soon as possible)
                if (null != eventLaunched)
                {
                    eventLaunched.Set();
                }
                m_Dev.DevLinUpdateSlaveResponse(parameters["Channel"], parameters["Response ID"], parameters["Response Data"]);
            }
            catch (Exception ex)
            {
                logger.WriteLine(ex.ToString());
                result[VERDICT] = TestStatus.FAILED.ToString();
                result[REASON] = ex.Message;
            }
            return result;
        }

        [Parameter("Channel", "string", "CAN0,CAN1,CAN2,CAN3,CAN4,CAN5,CAN6,CAN7,CAN8,CAN9,CAN10,CAN11,CAN12,CAN13,CAN14,CAN15")]
        [Parameter("CANFD", "bool", "true")]
        [Parameter("Master", "string", "false")]
        public string[] DevCanChanConfig(Dictionary<string, string> parameters,
            List<string> expectedParams, EventWaitHandle eventLaunched)
        {
            string[] result = { TestStatus.PASSED.ToString(), "" };

            try
            {
                // Please set the eventLaunched when the function is considered to have "launched" 
                // (but as soon as possible)
                if (null != eventLaunched)
                {
                    eventLaunched.Set();
                }
                m_Dev.DevCanChanConfig(parameters["Channel"], parameters["Baud"], parameters["Master"]);
            }
            catch (Exception ex)
            {
                logger.WriteLine(ex.ToString());
                result[VERDICT] = TestStatus.FAILED.ToString();
                result[REASON] = ex.Message;
            }
            return result;
        }

        [Parameter("Channel", "string", "CAN0,CAN1,CAN2,CAN3,CAN4,CAN5,CAN6,CAN7,CAN8,CAN9,CAN10,CAN11,CAN12,CAN13,CAN14,CAN15")]
        [Parameter("FrameID", "string", "0x8000")]
        [Parameter("Wait Timeout", "string", "50 mS")]
        //eg. [Parameter("IP", "String", "192.168.1.200")]
        public string[] DevCanSendMessage(Dictionary<string, string> parameters,
            List<string> expectedParams, EventWaitHandle eventLaunched)
        {
            string[] result = { TestStatus.PASSED.ToString(), "" };

            try
            {
                // Please set the eventLaunched when the function is considered to have "launched" 
                // (but as soon as possible)
                if (null != eventLaunched)
                {
                    eventLaunched.Set();
                }
                m_Dev.DevCanSendMessage(parameters["Channel"], parameters["Require ID"], parameters["Response Data"]);
            }
            catch (Exception ex)
            {
                logger.WriteLine(ex.ToString());
                result[VERDICT] = TestStatus.FAILED.ToString();
                result[REASON] = ex.Message;
            }
            return result;
        }
        [Parameter("Channel", "string", "CAN0,CAN1,CAN2,CAN3,CAN4,CAN5,CAN6,CAN7,CAN8,CAN9,CAN10,CAN11,CAN12,CAN13,CAN14,CAN15")]
        [Parameter("Notify ID", "string", "0x8000")] // or
        [Parameter("Wait Timeout", "string", "50 mS")]
        //eg. [Parameter("IP", "String", "192.168.1.200")]
        public string[] DevCanRecvMessage(Dictionary<string, string> parameters,
            List<string> expectedParams, EventWaitHandle eventLaunched)
        {
            string[] result = { TestStatus.PASSED.ToString(), "" };

            try
            {
                // Please set the eventLaunched when the function is considered to have "launched" 
                // (but as soon as possible)
                if (null != eventLaunched)
                {
                    eventLaunched.Set();
                }
                m_Dev.DevCanRecvMessage(parameters["Channel"], parameters["Require ID"], parameters["Response Data"]);
            }
            catch (Exception ex)
            {
                logger.WriteLine(ex.ToString());
                result[VERDICT] = TestStatus.FAILED.ToString();
                result[REASON] = ex.Message;
            }
            return result;
        }

        [Parameter("Channel", "string", "LIN0")]
        [Parameter("Baud", "string", "19200")]
        [Parameter("Master", "string", "false")]
        public string[] DevEthChanConfig(Dictionary<string, string> parameters,
            List<string> expectedParams, EventWaitHandle eventLaunched)
        {
            string[] result = { TestStatus.PASSED.ToString(), "" };

            try
            {
                // Please set the eventLaunched when the function is considered to have "launched" 
                // (but as soon as possible)
                if (null != eventLaunched)
                {
                    eventLaunched.Set();
                }
                m_Dev.DevEthChanConfig(parameters["Channel"], parameters["Baud"], parameters["Master"]);
            }
            catch (Exception ex)
            {
                logger.WriteLine(ex.ToString());
                result[VERDICT] = TestStatus.FAILED.ToString();
                result[REASON] = ex.Message;
            }
            return result;
        }

        [Parameter("Notify ID", "string", "0x8000")] // or
        [Parameter("Wait Timeout", "string", "50 mS")]
        //eg. [Parameter("IP", "String", "192.168.1.200")]
        public string[] DevEthSendUdpMessage(Dictionary<string, string> parameters,
            List<string> expectedParams, EventWaitHandle eventLaunched)
        {
            string[] result = { TestStatus.PASSED.ToString(), "" };

            try
            {
                // Please set the eventLaunched when the function is considered to have "launched" 
                // (but as soon as possible)
                if (null != eventLaunched)
                {
                    eventLaunched.Set();
                }
                m_Dev.DevEthSendUdpMessage(parameters["Channel"], parameters["Require ID"], parameters["Response Data"]);
            }
            catch (Exception ex)
            {
                logger.WriteLine(ex.ToString());
                result[VERDICT] = TestStatus.FAILED.ToString();
                result[REASON] = ex.Message;
            }
            return result;
        }
        [Parameter("Notify ID", "string", "0x8000")] // or
        [Parameter("Wait Timeout", "string", "50 mS")]
        //eg. [Parameter("IP", "String", "192.168.1.200")]
        public string[] DevEthRecvUdpMessage(Dictionary<string, string> parameters,
            List<string> expectedParams, EventWaitHandle eventLaunched)
        {
            string[] result = { TestStatus.PASSED.ToString(), "" };

            try
            {
                // Please set the eventLaunched when the function is considered to have "launched" 
                // (but as soon as possible)
                if (null != eventLaunched)
                {
                    eventLaunched.Set();
                }
                m_Dev.DevEthReceiveUdpMessage(parameters["Channel"], parameters["Require ID"], parameters["Response Data"]);
            }
            catch (Exception ex)
            {
                logger.WriteLine(ex.ToString());
                result[VERDICT] = TestStatus.FAILED.ToString();
                result[REASON] = ex.Message;
            }
            return result;
        }

        #endregion

        bool CheckExecCondition(string[] result)
        {
            bool canexec = true;
            if (!m_Dev.IsOpen)
            {
                result[VERDICT] = TestStatus.FAILED.ToString();
                result[REASON] = "Please Calll \"NetIfOpen\" First! ";
                canexec = false;
            }
            //if(!canexec)
            //{
            //    result[VERDICT] = TestStatus.FAILED.ToString();
            //    result[REASON] = ex.Message;
            //}
            return canexec;
        }
    }
}
