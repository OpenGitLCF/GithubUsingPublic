using System;
using System.Collections.Concurrent;
using System.Text;
using System.Threading;

namespace SerialNetIf
{

    //public delegate void OnReceivedResponse(object sender, uint id, byte[] data);
    class SerialNetIfDev
    {
        SerialTinCAN m_Serial = new SerialTinCAN();
        //public event OnReceivedResponse ReceivedResponse;
        #region("Interface")
        public bool Open(string port, int baud)
        {
            m_Serial.OnMessageReceived += TinCanCommandReceived;
            if (!m_Serial.OpenPort(port, baud))
                return false;
            return Start();
        }
        public bool Close()
        {
            m_Serial.OnMessageReceived -= TinCanCommandReceived;
            bool ret = m_Serial.ClosePort();
            Stop();
            return ret;
        }
        public bool IsOpen { get { return m_Serial.IsOpen; } }
        public bool CleanReceivedMessage(uint id)
        {
            
            if(0x1FFFFFFF == id)
            {
                foreach(var kv in m_recvMsgDic)
                {
                    kv.Value.ResetEvent();
                }
            }
            else
            {
                if (!m_recvMsgDic.ContainsKey(id))
                {
                    return true;
                }
                MsgResponseSlot slot = null;
                slot = m_recvMsgDic[id];
                slot.ResetEvent();
            }
            return true;
        }
        public bool SendCmdWaitResponse(uint id, byte[] sData, double tmo, out byte[] response)
        {
            response = null;
            if (!SendMessage(id, sData))
                return false;

            NetIfMessage resp = null;
            if (EWaitResponseResult.Success != WaitMessage(id, (int)(tmo*1000), out resp))
                return false;

            if (null != resp)
                response = resp.Data;

            return true;
        }

        public bool SendMessage(uint id, byte[] data)
        {
            NetIfMessage msg = new NetIfMessage(id, data);
            MsgResponseSlot slot = null; ;
            if (!m_recvMsgDic.ContainsKey(id))
            {
                m_recvMsgDic[id] = slot = new MsgResponseSlot(id);
            }
            else
            {
                slot = m_recvMsgDic[id];
            }
            if (slot.IsSending)
                return false;
            slot.StartSending();
            sendQue.Enqueue(msg);
            return true;
        }

        public bool ReadResponse(uint id, out byte[] response)
        {
            response = null;

            MsgResponseSlot slot = null;
            if (!m_recvMsgDic.ContainsKey(id))
            {
                return false;
            }
            else
            {
                slot = m_recvMsgDic[id];
            }
            if (slot.IsReceived)
            {
                NetIfMessage resp = slot.Msg;
                if (null != resp)
                {
                    // Copy ???
                    response = resp.Data;
                }
                return true;
            }
            return false;
        }

        public bool WaitNotify(uint id, double tmo, out byte[] response)
        {
            response = null;
            NetIfMessage resp = null;
            if (EWaitResponseResult.Success != WaitMessage(id, (int)(tmo*1000), out resp))
                return false;

            if (null != resp)
                response = resp.Data;
            return true;
        }
        #endregion()

        public bool Start()
        {
            ContinueRecv = true;
            thdRecvProcess = new Thread(RecvProcessThread);
            thdRecvProcess.IsBackground = true;
            thdRecvProcess.Start();

            ContinueSend = true;
            thdSendProcess = new Thread(SendProcessThread);
            thdSendProcess.IsBackground = true;
            thdSendProcess.Start();

            //if (!CurrentChan.IsOpen)
            //{
            //    ConfigChanDlg cfg = new ConfigChanDlg();
            //    cfg.MsgTransceiver = this;
            //    cfg.Show();
            //}
            return true;
        }

        public bool Stop()
        {
            if (null != thdRecvProcess && thdRecvProcess.IsAlive)
            {
                ContinueRecv = false;
                Thread.Sleep(50);
                thdRecvProcess.Abort();
            }

            if (null != thdSendProcess && thdSendProcess.IsAlive)
            {
                ContinueSend = false;
                Thread.Sleep(50);
                thdSendProcess.Abort();
            }

            if (m_Serial.IsOpen)
            {
                m_Serial.ClosePort();
            }
            return true;
        }
        void LogMsg(uint id, byte[] data, bool bSend, bool bPass = true)
        {
            StringBuilder sb = new StringBuilder();
            if (bSend)
                sb.Append(" Tx Message ");
            else
                sb.Append(" Rx Message ");

            sb.AppendFormat("{0:X8} {1:d3} [ ", id, null!= data?data.Length:0);
            if (null != data)
            {
                foreach (byte d in data)
                {
                    sb.AppendFormat("{0:X2}", d);
                }
            }
            if (bPass)
            {
                sb.Append(" ]");
            }
            else
            {
                sb.Append(" ] Failed !");
            }
            SerialNetIf.logger.WriteLine(sb.ToString());
        }
        #region Recv
        void TinCanCommandReceived(object sender, NetIfMessage arg)
        {
            LogMsg(arg.Id, arg.Data, false);
            recvQue.Enqueue(arg);
        }

        protected void DispatchMessage(NetIfMessage msg)
        {
            MsgResponseSlot slot = null;
            if (m_recvMsgDic.ContainsKey(msg.Id))
            {
                slot = m_recvMsgDic[msg.Id];
                slot.Msg = msg;
            }
            else
            {
                m_recvMsgDic[msg.Id] = slot = new MsgResponseSlot(msg.Id);
                slot = m_recvMsgDic[msg.Id];
                slot.Msg = msg;
            }
        }
        void RecvProcessThread()
        {
            NetIfMessage rf = null;
            while (ContinueRecv)
            {
                if (!recvQue.TryDequeue(out rf))
                {
                    Thread.Sleep(0);
                    continue;
                }
                DispatchMessage(rf);
            }
        }

        ConcurrentQueue<NetIfMessage> recvQue = new ConcurrentQueue<NetIfMessage>();
        Thread thdRecvProcess;
        volatile bool ContinueRecv = false;
        #endregion Recv

        #region Wait
        protected enum EWaitResponseResult
        {
            Success = 0,
            Timeout,
            Error
        }

        ConcurrentDictionary<uint, MsgResponseSlot> m_recvMsgDic = new ConcurrentDictionary<uint, MsgResponseSlot>();
        protected EWaitResponseResult WaitMessage(uint id, int tmo, out NetIfMessage res)
        {
            res = null;
            MsgResponseSlot slot = null;
            if (!m_recvMsgDic.ContainsKey(id))
            {
                m_recvMsgDic[id] = slot = new MsgResponseSlot(id);
                //return EWaitResponseResult.Error;
            }
            else
            {
                slot = m_recvMsgDic[id];
            }

            if (slot.IsReceived)
            {
                res = slot.Msg;
                return EWaitResponseResult.Success;
            }


            if (slot.WaitMsg(tmo))
            {
                res = slot.Msg;
                return EWaitResponseResult.Success;
            }
            return EWaitResponseResult.Timeout;
        }

        //protected EWaitResponseResult WaitNetIfNotify(uint id, int tmo, out NetIfMessage nty)
        //{
        //    nty = null;
        //    MsgResponseSlot slot = null;
        //    if (!m_recvMsgDic.ContainsKey(id))
        //    {
        //        m_recvMsgDic[id] = slot = new MsgResponseSlot(id);
        //        //return EWaitResponseResult.Error;
        //    }
        //    else
        //    {
        //        slot = m_recvMsgDic[id];
        //    }

        //    if (slot.IsReceived)
        //    {
        //        nty = slot.Msg;
        //        return EWaitResponseResult.Success;
        //    }

        //    if (slot.WaitMsg(tmo))
        //    {
        //        nty = slot.Msg;
        //        return EWaitResponseResult.Success;
        //    }
        //    return EWaitResponseResult.Timeout;
        //}
        #endregion

        #region Send
        ConcurrentQueue<NetIfMessage> sendQue = new ConcurrentQueue<NetIfMessage>();
        Thread thdSendProcess;
        volatile bool ContinueSend = false;
        //protected bool SendMessage(uint id, byte[] data)
        //{

        //}

        void SendProcessThread()
        {
            NetIfMessage rf = null;
            bool AnyMsgSent = false;
            while (ContinueSend)
            {
                AnyMsgSent = false;
                while (sendQue.TryDequeue(out rf))
                {
                    SendToSerial(rf);
                    Thread.Sleep(0);
                    AnyMsgSent = true;
                }

                if (AnyMsgSent)
                    continue;
            }
        }
        bool SendToSerial(NetIfMessage cmd)
        {
            bool ret = m_Serial.SendMessage(cmd.Id, cmd.Data);
            LogMsg(cmd.Id, cmd.Data, true, ret);
            return ret;
        }
        #endregion Send
    }
    class MsgResponseSlot : IDisposable
    {
        EventWaitHandle m_respEvent = null;
        NetIfMessage m_msg = null;
        int m_nLastRecvTick;
        int m_nRecvTick;
        volatile bool m_bSending = false;

        public uint Id { get; private set; }
        public bool IsReceived { get { return m_nLastRecvTick < m_nRecvTick; } }
        public bool IsSending { get { return m_bSending; } }
        public NetIfMessage Msg
        {
            get
            {
                lock (this)
                {
                    m_nLastRecvTick = m_nRecvTick;
                    return m_msg;
                }
            }
            set
            {
                lock (this)
                {
                    m_msg = value;
                    m_nRecvTick = Environment.TickCount;
                    m_bSending = false;
                    m_respEvent.Set();
                }
            }
        }
        public MsgResponseSlot(uint id)
        {
            Id = id;
            m_respEvent = new EventWaitHandle(false, EventResetMode.AutoReset);
            m_nLastRecvTick = m_nRecvTick = 0;
            m_bSending = false;
        }
        public void Dispose()
        {
            //lock (this)
            //{
            if (m_respEvent != null)
            {
                // m_respEvent.Reset();
                m_respEvent.Dispose();
                m_respEvent = null;
            }
            //}
        }
        public void StartSending() { m_bSending = false; }

        public void ResetEvent() 
        {
            NetIfMessage msg = Msg;
            m_respEvent.Reset(); 
        }
        public bool WaitMsg(int tmo)
        {
            //if (m_respEvent == null)
            //    return false;
            // Open();
            bool ret = m_respEvent.WaitOne(tmo);

            return ret;
        }
    }
}
