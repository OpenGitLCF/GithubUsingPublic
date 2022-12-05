//using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
//using System.Linq;

using System.Text;
using System.Threading;
//using System.Threading.Tasks;

namespace SerialNetIf
{
    class NetIfDevEx : SerialNetIfDev
    {
        #region("Interface")
        public bool DutCheckConnect(int dut)
        {
            return true;
        }
        public bool DutCheckReset(int dut)
        {
            return true;
        }
        #endregion()

        #region Device wrapper function
        public bool DevLinChanConfig(string linid, string baud, string master)
        {
            return true;
        }
        public bool DevLinSendMasterRequire(string linid, string baud, string master)
        {
            return true;
        }
        public bool DevLinUpdateSlaveResponse(string linid, string baud, string master)
        {
            return true;
        }
        public bool DevCanChanConfig(string linid, string baud, string master)
        {
            return true;
        }
        public bool DevCanSendMessage(string linid, string baud, string master)
        {
            return true;
        }
        public bool DevCanRecvMessage(string linid, string baud, string master)
        {
            return true;
        }
        public bool DevEthChanConfig(string linid, string baud, string master)
        {
            return true;
        }
        public bool DevEthSendUdpMessage(string linid, string baud, string master)
        {
            return true;
        }
        public bool DevEthReceiveUdpMessage(string linid, string baud, string master)
        {
            return true;
        }
        #endregion
    }
   
}
