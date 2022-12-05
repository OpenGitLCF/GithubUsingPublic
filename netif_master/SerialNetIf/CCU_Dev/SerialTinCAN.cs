using System;
using System.Collections.Concurrent;
using System.Collections.Extern;
//using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO.Ports;
//using System.Linq;
using System.Text;
using System.Threading;
//using System.Threading.Tasks;

namespace SerialNetIf
{

    class SerialTinCAN
    {
        enum EMsgIDType
        {
            STD_ID = 0x00,
            EXT_ID = 0x01,
            CNTRL_ID = 0x02,
            J1850_ID = 0x03
        }
        enum TiCAN_FrameType : byte
        {
            FT_QUICK_CAN_MSG_ID = 0x00,
            FT_START_CAN_MULTI_PACKET_ID = 0x01,
            FT_CONTINUE_CAN_MULTI_PACKET_ID = 0x02,
            FT_END_CAN_MULTI_PACKET_ID = 0x03,
            FT_FROM_TCU_PACKET_ID = 0x04,
        }
        public const byte SYN = 0x16;
        enum EMaxSize : int
        {
            TransportPacketMaxSize = 259,
            PacketPayloadMaxSize = 255,
            MessageDataMaxSize = 250,
            MultiPacketFirstFrameSize = 248,
        }
        SerialPort m_Serial = null;
        public SerialTinCAN()
        {
            //bufRecv.Event += RingBufferError;
        }
        #region("Public Interface")
        public bool OpenPort(string port, int baud)
        {
            if (null != m_Serial)
            {
                if (m_Serial.IsOpen)
                {
                    m_Serial.Close();
                    m_Serial.Dispose();
                }
            }
            m_Serial = new SerialPort(port, baud, Parity.None, 8, StopBits.One);
            m_Serial.Open();
            m_Serial.DiscardInBuffer();
            m_Serial.DiscardOutBuffer();

            m_Serial.DataReceived += OnSerialDataReceived;
            return m_Serial.IsOpen;
        }
        public bool ClosePort()
        {
            if (null != m_Serial)
            {
                m_Serial.DataReceived -= OnSerialDataReceived;
                m_Serial.Close();
            }

            return true;
        }
        public bool IsOpen { get { return null == m_Serial ? false : m_Serial.IsOpen; } }
        public bool SendMessage(uint id, byte[] msg)
        {
            return SendMessage(id, msg, (null == msg) ? 0 : msg.Length);
        }
        #endregion

        #region("Send")
        byte[] m_SendFrameBuf = new byte[(int)EMaxSize.TransportPacketMaxSize];
        byte[] m_SendPayloadBuff = new byte[(int)EMaxSize.PacketPayloadMaxSize];
        bool SendMessage(uint id, byte[] msg, int NumBytes)
        {
            if (NumBytes > (int)EMaxSize.MessageDataMaxSize)
            {
                /* break into multiple packets */
                ushort num_packets = (ushort)((int)((NumBytes + (int)EMaxSize.MessageDataMaxSize + 2 - 1) / (int)EMaxSize.MessageDataMaxSize));

                int posSend = 0;
                /* first packet */
                SendFrame(id, TiCAN_FrameType.FT_START_CAN_MULTI_PACKET_ID, num_packets, msg, posSend, (int)EMaxSize.MultiPacketFirstFrameSize);
                posSend += (int)EMaxSize.MultiPacketFirstFrameSize;

                /* continue packet */
                while (--num_packets > 1)
                {
                    SendFrame(id, TiCAN_FrameType.FT_CONTINUE_CAN_MULTI_PACKET_ID, num_packets, msg, posSend, (int)EMaxSize.MessageDataMaxSize);
                    posSend += (int)EMaxSize.MessageDataMaxSize;
                }

                /* last packet */
                SendFrame(id, TiCAN_FrameType.FT_END_CAN_MULTI_PACKET_ID, num_packets, msg, posSend, NumBytes - posSend);
            }
            else /* send as a single packet */
            {
                SendFrame(id, TiCAN_FrameType.FT_QUICK_CAN_MSG_ID, 1, msg, 0, NumBytes);
            }

            return true;
        }
        bool SendFrame(uint id, TiCAN_FrameType frametype, ushort frameno, byte[] data, int off, int len)
        {
            uint payload_len = 5;
            id = (id&0x1FFFFFF0) | (uint)frametype;
            m_SendPayloadBuff[0] = (byte)EMsgIDType.EXT_ID;
            m_SendPayloadBuff[1] = (byte)((id >> 24) & 0x000000ff);
            m_SendPayloadBuff[2] = (byte)((id >> 16) & 0x000000ff);
            m_SendPayloadBuff[3] = (byte)((id >> 8) & 0x000000ff);
            m_SendPayloadBuff[4] = (byte)(id & 0x000000ff);
            if (TiCAN_FrameType.FT_START_CAN_MULTI_PACKET_ID == frametype)
            {
                m_SendPayloadBuff[5] = (byte)(frameno >> 8);
                m_SendPayloadBuff[6] = (byte)(frameno & 0x00ff);
                Array.Copy(data, off, m_SendPayloadBuff, 7, len);
                payload_len += (uint)(2 + len);
            }
            else
            {
                if (null != data)
                {
                    Array.Copy(data, off, m_SendPayloadBuff, 5, len);
                    payload_len += (uint)(len);
                }
            }
            return Tx(m_SendPayloadBuff, payload_len);
        }
        bool Tx(byte[] data, uint num_bytes)
        {
            uint temp;
            byte checksum = 0;

            m_SendFrameBuf[0] = SYN;
            m_SendFrameBuf[1] = (byte)num_bytes;
            temp = ~num_bytes;
            m_SendFrameBuf[2] = (byte)temp;

            for (int index = 0; index < (int)num_bytes; index++)
            {
                m_SendFrameBuf[index + 3] = data[index];
                checksum += data[index];
            }

            m_SendFrameBuf[num_bytes + 3] = (byte)~checksum;

            try
            {
                m_Serial.Write(m_SendFrameBuf, 0, (int)(num_bytes + 4));
            }
            catch (Exception ex)
            {
                SerialNetIf.logger.WriteLine(ex.ToString());
                return false;
            }

            return true;
        }
        #endregion

        #region("Recv")


        byte[] m_RecvBuffer = new byte[1024];
        RingBuffer<byte> m_RecvFrameBuff = new RingBuffer<byte>(1024);
        byte[] m_RecvPayloadBuff = new byte[(int)EMaxSize.PacketPayloadMaxSize];
        class MultiFrameMessage
        {
            public uint Id { get; set; }
            public uint Packet_cnt { get; set; }
            public uint Packets_in_msg { get; set; }
            public List<byte> Data { get; set; }

            public MultiFrameMessage() { }
            public void StartFrame(uint id, byte[] data, int off, int len)
            {
                Id = id;
                /* the first 2 bytes of data are nmbr of packets in msg */
                Packets_in_msg = data[off];
                Packets_in_msg <<= 8;
                Packets_in_msg += data[off + 1];
                Data = new List<byte>();// resires.packets_in_msg];

                for (int i = off + 2; i < len; i++)
                    Data.Add(data[i]);
                Packet_cnt = 1;
            }

            public void ContinueFrame(byte[] data, int off, int len)
            {
                for (int i = off; i < len; i++)
                    Data.Add(data[i]);
                //res.data.AddRange(data);

                /* indicate another packet has been added */
                Packet_cnt++;
            }
        }
        PackHashPool<MultiFrameMessage> PacketPool = new PackHashPool<MultiFrameMessage>();

        void OnSerialDataReceived(object sender, SerialDataReceivedEventArgs e)
        {
            int bytesRead = m_Serial.Read(m_RecvBuffer, 0, m_RecvBuffer.Length);

            SerialNetIf.logger.WriteLine("===================Rx \r\n" + StringUtil.ByteArrayToHexString(m_RecvBuffer, bytesRead));
            SerialNetIf.logger.WriteLine("===================Thread ID = " + Thread.CurrentThread.ManagedThreadId);

            // Do something with workingString
            if (bytesRead > 0)
            {
                try
                {
                    if (BufferRecvData(m_RecvBuffer, bytesRead))
                    {
                        ParseRecvData();
                    }
                }
                catch (Exception ex)
                {
                    SerialNetIf.logger.WriteLine(ex.ToString());
                }
            }
        }
        bool BufferRecvData(byte[] data, int len)
        {
            if (IsCompletePackage(data, len))
            {
                SerialNetIf.logger.WriteLine("===================CompletePackage========================");
                m_RecvFrameBuff.Clear();
            }
            m_RecvFrameBuff.Append(data, len);
            return true;
        }

        bool IsCompletePackage(byte[] data, int len)
        {
            if (len < 9)
                return false;
            if (data[0] != SerialTinCAN.SYN)
                return false;
            if (data[1] + 4 > len)
                return false;
            if (data[1] != (byte)(~data[2]))
                return false;
            //byte cs = 0;
            //for (int i = 0; i < data[1]; i++)
            //{
            //    cs += data[3 + i];
            //}
            //cs = (byte)(~cs) ;
            //if (cs != data[data[1] + 3])
            //    return false;
            return true;
        }

        void ParseRecvData()
        {
            while (m_RecvFrameBuff.Size > 0)
            {
                if (m_RecvFrameBuff.Peek(0) != SerialTinCAN.SYN)
                {
                    m_RecvFrameBuff.Popup(1);
                    continue;
                }
                int len = m_RecvFrameBuff.Size - 4;
                if (len < 9)
                {
                    SerialNetIf.logger.WriteLine("===================ParseRecvData len=" + len.ToString());
                    return;
                }
                if (m_RecvFrameBuff.Peek(1) > len)
                {
                    SerialNetIf.logger.WriteLine("===================ParseRecvData data len=" + m_RecvFrameBuff.Peek(1).ToString());
                    return;
                }
                if (m_RecvFrameBuff.Peek(1) != (byte)(~m_RecvFrameBuff.Peek(2)))
                {
                    SerialNetIf.logger.WriteLine("===================ParseRecvData Neg Len = " + m_RecvFrameBuff.Peek(2).ToString());
                    m_RecvFrameBuff.Popup(1);
                    continue;
                }
                len = m_RecvFrameBuff.Peek(1);
                byte cs = 0;
                for (int i = 0; i < len; i++)
                {
                    cs += m_RecvFrameBuff.Peek(3 + i);
                }
                cs = (byte)(~cs);
                if (cs != m_RecvFrameBuff.Peek(len + 3))
                {
                    SerialNetIf.logger.WriteLine("===================ParseRecvData CRC = " + cs.ToString());
                    m_RecvFrameBuff.Popup(1);
                    continue;
                }
                m_RecvFrameBuff.Popup(3);
                m_RecvPayloadBuff = m_RecvFrameBuff.Popup(len);
                m_RecvFrameBuff.Popup(1);

                ParsePackage(m_RecvPayloadBuff, len);
            }
        }
        bool ParsePackage(byte[] data, int len)
        {
            // byte msg_type, cnt;
            byte IDType = data[0];   // first byte is can id type
            if (IDType != (byte)EMsgIDType.EXT_ID)
            {
                SerialNetIf.logger.WriteLine("Packet Payload CAN ID Type Error = " + IDType.ToString());
                return false;
            }
            // get the id
            uint id = (uint)((data[1] << 24) | (data[2] << 16) | (data[3] << 8) | data[4]);

            /* get the message protocol info */
            TiCAN_FrameType msg_type = (TiCAN_FrameType)(id & 0x00000003);
            bool isResp = (id & (uint)TiCAN_FrameType.FT_FROM_TCU_PACKET_ID) != 0;
            /* get rid of the protocol info from the id */
            id &= 0xfffffff0;

            return ParsePacket(id, msg_type, isResp, data, 5);
        }

        bool ParsePacket(uint id, TiCAN_FrameType msg_type, bool isResp, byte[] data, int off)
        {
            SerialNetIf.logger.WriteLine("===================ParsePacket=" + id.ToString() + msg_type.ToString() + off.ToString());

            switch (msg_type)
            {
                case TiCAN_FrameType.FT_QUICK_CAN_MSG_ID:
                    {
                        NetIfMessage rsp = new NetIfMessage(id, data, off, data.Length - off);
                        FireRecvEvent(rsp);
                    }
                    break;
                case TiCAN_FrameType.FT_START_CAN_MULTI_PACKET_ID:
                    {
                        MultiFrameMessage res = PacketPool.Get(id);
                        res.StartFrame(id, data, off, data.Length);
                    }
                    break;

                case TiCAN_FrameType.FT_CONTINUE_CAN_MULTI_PACKET_ID:
                case TiCAN_FrameType.FT_END_CAN_MULTI_PACKET_ID:
                    {
                        MultiFrameMessage res = PacketPool.Get(id);
                        if (null == res)
                        {
                            ////////////////////////////////////////////////
                            /////Error
                            SerialNetIf.logger.WriteLine("ParsePacket Error message ID = " + id.ToString("X"));
                            break;
                        }
                        res.ContinueFrame(data, off, data.Length);
                        if (msg_type == TiCAN_FrameType.FT_END_CAN_MULTI_PACKET_ID)
                        {
                            if (res.Packet_cnt == res.Packets_in_msg)
                            {
                                NetIfMessage rsp = new NetIfMessage(res.Id, res.Data.ToArray());
                                FireRecvEvent(rsp);

                                PacketPool.Release(res.Id);
                            }
                        }
                    }
                    break;
                default:
                    {
                        ////////////////////////////////////////////////
                        /////Error
                        SerialNetIf.logger.WriteLine("ParsePacket Error message type = " + msg_type.ToString("X"));
                    }
                    break;
            }
            return true;
        }
        public event TinCanResponseReceived OnMessageReceived;
        protected void FireRecvEvent(NetIfMessage arg)
        {
            if (OnMessageReceived != null)
            {
                OnMessageReceived(this, arg);
            }
        }
        #endregion

    }

    public class PackHashPool<T> where T : new()
    {
        private readonly ConcurrentBag<T> items = new ConcurrentBag<T>();
        private readonly ConcurrentDictionary<uint, T> map = new ConcurrentDictionary<uint, T>();
        private int counter = 0;
        private int MAX = 10;
        public void Release(T item)
        {
            if (counter < MAX)
            {
                items.Add(item);
                counter++;
            }
        }
        public T Get()
        {
            T item;
            if (items.TryTake(out item))
            {
                counter--;
                return item;
            }
            else
            {
                T obj = new T();
                items.Add(obj);
                counter++;
                return obj;
            }
        }
        public T Get(uint idx)
        {
            if (map.ContainsKey(idx))
            {
                return map[idx];
            }
            else
            {
                T t = Get();
                map.TryAdd(idx, t);
                return t;
            }
        }

        public bool Find(uint idx)
        {
            return map.ContainsKey(idx);
        }

        public void Release(uint idx)
        {
            T o = default(T);
            if (map.TryRemove(idx, out o))
            {
                Release(o);
            }
        }
    }
    public class NetIfMessage
    {
        public NetIfMessage()
        {

        }
        public NetIfMessage(uint id, byte[] data)
        {
            Id = id;
            if (null != data)
            {
                Data = new byte[data.Length];
                Array.Copy(data, 0, Data, 0, data.Length);
            }
            else
            {
                Data = null;
            }
        }
        public NetIfMessage(uint id, byte[] data, int off, int len)
        {
            Id = id;
            if (null != data)
            {
                Data = new byte[len];
                Array.Copy(data, off, Data, 0, len);
            }
            else
            {
                Data = null;
            }
        }

        public uint Id { get; set; }
        public byte[] Data { get; set; }
        //public TimeSpan Cycle { get; set; }
        //public DateTime LastSend { get; set; }
    }
    public delegate void TinCanResponseReceived(object sender, NetIfMessage arg);
}
