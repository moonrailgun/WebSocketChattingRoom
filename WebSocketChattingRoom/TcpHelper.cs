using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;

namespace WebSocketChattingRoom
{
    public class TcpHelper
    {
        private Dictionary<Socket, ClientInfo> clientPool = new Dictionary<Socket, ClientInfo>();
        private List<SocketMessage> msgPool = new List<SocketMessage>();
        private bool isClear = true;

        private Thread currentServerSocketThread;
        private Thread currentBroadcastThread;

        /// <summary>
        /// 启动服务器，监听客户端请求
        /// </summary>
        /// <param name="port">服务器端进程口号</param>
        public void Run(int port)
        {
            currentServerSocketThread = new Thread(() =>
            {
                Socket server = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                server.Bind(new IPEndPoint(IPAddress.Any, port));
                server.Listen(10);
                server.BeginAccept(new AsyncCallback(Accept), server);
            });

            currentServerSocketThread.Start();
            LogsSystem.Instance.Print("服务器监听线程打开完毕");
            Broadcast();
        }

        /// <summary>
        /// 中断SOCKET连接
        /// </summary>
        public void Abort()
        {
            if (currentServerSocketThread != null || currentBroadcastThread != null)
            {
                try
                {
                    //清空内存
                    this.clientPool.Clear();
                    this.msgPool.Clear();
                    this.isClear = true;

                    this.currentServerSocketThread.Abort();
                    this.currentBroadcastThread.Abort();
                    this.currentServerSocketThread = null;
                    this.currentBroadcastThread = null;

                    LogsSystem.Instance.Print("服务器线程成功关闭");
                }
                catch (Exception ex)
                {
                    LogsSystem.Instance.Print("服务器没有被正常关闭:" + ex.ToString());
                }
            }
            else
            {
                LogsSystem.Instance.Print("服务器线程尚未打开。");
            }

        }

        /// <summary>
        /// 在独立线程中不停地向所有客户端广播消息
        /// </summary>
        private void Broadcast()
        {
            currentBroadcastThread = new Thread(() =>
            {
                while (true)
                {
                    if (!isClear)
                    {
                        byte[] msg = PackageServerData(msgPool[0]);
                        foreach (KeyValuePair<Socket, ClientInfo> cs in clientPool)
                        {
                            Socket client = cs.Key;
                            if (client.Poll(10, SelectMode.SelectWrite))
                            {
                                client.Send(msg, msg.Length, SocketFlags.None);
                            }
                        }
                        msgPool.RemoveAt(0);
                        isClear = msgPool.Count == 0 ? true : false;
                    }
                }
            });

            currentBroadcastThread.Start();
            LogsSystem.Instance.Print("服务器广播线程打开完毕");
        }

        /// <summary>
        /// 处理客户端连接请求,成功后把客户端加入到clientPool
        /// </summary>
        /// <param name="result">Result.</param>
        private void Accept(IAsyncResult result)
        {
            Socket server = result.AsyncState as Socket;
            Socket client = server.EndAccept(result);
            try
            {
                //处理下一个客户端连接
                server.BeginAccept(new AsyncCallback(Accept), server);
                byte[] buffer = new byte[1024];
                //接收客户端消息
                client.BeginReceive(buffer, 0, buffer.Length, SocketFlags.None, new AsyncCallback(Recieve), client);
                ClientInfo info = new ClientInfo();
                info.Id = client.RemoteEndPoint;
                info.handle = client.Handle;
                info.buffer = buffer;
                //把客户端存入clientPool
                this.clientPool.Add(client, info);
                LogsSystem.Instance.Print(string.Format("IP[{0}]连接到服务器", client.RemoteEndPoint));
            }
            catch (Exception ex)
            {
                LogsSystem.Instance.Print("出错:" + ex.ToString(), LogLevel.ERROR);
            }
        }

        /// <summary>
        /// 处理客户端发送的消息，接收成功后加入到msgPool，等待广播
        /// </summary>
        /// <param name="result">Result.</param>
        private void Recieve(IAsyncResult result)
        {
            Socket client = result.AsyncState as Socket;

            if (client == null || !clientPool.ContainsKey(client))
            {
                return;
            }

            try
            {
                int length = client.EndReceive(result);
                byte[] buffer = clientPool[client].buffer;

                //接收消息
                client.BeginReceive(buffer, 0, buffer.Length, SocketFlags.None, new AsyncCallback(Recieve), client);
                string msg = Encoding.UTF8.GetString(buffer, 0, length);

                if (!clientPool[client].IsHandShaked && msg.Contains("Sec-WebSocket-Key"))
                {
                    client.Send(PackageHandShakeData(buffer, length));
                    clientPool[client].IsHandShaked = true;
                    return;
                }

                msg = AnalyzeClientData(buffer, length);

                SocketMessage sm = new SocketMessage();
                sm.Client = clientPool[client];
                sm.Time = DateTime.Now;

                Regex reg = new Regex(@"{<(.*?)>}");
                Match m = reg.Match(msg);
                if (m.Value != "")
                { //处理客户端传来的用户名
                    clientPool[client].NickName = Regex.Replace(m.Value, @"{<(.*?)>}", "$1");
                    sm.isLoginMessage = true;
                    sm.Message = "login!";
                    LogsSystem.Instance.Print(string.Format("[{0}]登陆到聊天室", client.RemoteEndPoint));
                }
                else
                { //处理客户端传来的普通消息
                    sm.isLoginMessage = false;
                    sm.Message = msg;
                    LogsSystem.Instance.Print(string.Format("[{0}]:{2}", client.RemoteEndPoint, DateTime.Now, sm.Message));
                }
                msgPool.Add(sm);
                isClear = false;

            }
            catch
            {
                //把客户端标记为关闭，并在clientPool中清除
                client.Disconnect(true);
                LogsSystem.Instance.Print(string.Format("用户[{0}]断开了连接", clientPool[client].Name));
                clientPool.Remove(client);
            }
        }

        /// <summary>
        /// 打包服务器握手数据
        /// </summary>
        /// <returns>The hand shake data.</returns>
        /// <param name="handShakeBytes">Hand shake bytes.</param>
        /// <param name="length">Length.</param>
        private byte[] PackageHandShakeData(byte[] handShakeBytes, int length)
        {
            string handShakeText = Encoding.UTF8.GetString(handShakeBytes, 0, length);
            string key = string.Empty;
            Regex reg = new Regex(@"Sec\-WebSocket\-Key:(.*?)\r\n");
            Match m = reg.Match(handShakeText);
            if (m.Value != "")
            {
                key = Regex.Replace(m.Value, @"Sec\-WebSocket\-Key:(.*?)\r\n", "$1").Trim();
            }

            byte[] secKeyBytes = SHA1.Create().ComputeHash(
                                     Encoding.ASCII.GetBytes(key + "258EAFA5-E914-47DA-95CA-C5AB0DC85B11"));
            string secKey = Convert.ToBase64String(secKeyBytes);

            var responseBuilder = new StringBuilder();
            responseBuilder.Append("HTTP/1.1 101 Switching Protocols" + "\r\n");
            responseBuilder.Append("Upgrade: websocket" + "\r\n");
            responseBuilder.Append("Connection: Upgrade" + "\r\n");
            responseBuilder.Append("Sec-WebSocket-Accept: " + secKey + "\r\n\r\n");

            return Encoding.UTF8.GetBytes(responseBuilder.ToString());
        }

        /// <summary>
        /// 解析客户端发送来的数据
        /// </summary>
        /// <returns>The data.</returns>
        /// <param name="recBytes">Rec bytes.</param>
        /// <param name="length">Length.</param>
        private string AnalyzeClientData(byte[] recBytes, int length)
        {
            if (length < 2)
            {
                return string.Empty;
            }

            bool fin = (recBytes[0] & 0x80) == 0x80; // 1bit，1表示最后一帧  
            if (!fin)
            {
                return string.Empty;// 超过一帧暂不处理 
            }

            bool mask_flag = (recBytes[1] & 0x80) == 0x80; // 是否包含掩码  
            if (!mask_flag)
            {
                return string.Empty;// 不包含掩码的暂不处理
            }

            int payload_len = recBytes[1] & 0x7F; // 数据长度  

            byte[] masks = new byte[4];
            byte[] payload_data;

            if (payload_len == 126)
            {
                Array.Copy(recBytes, 4, masks, 0, 4);
                payload_len = (UInt16)(recBytes[2] << 8 | recBytes[3]);
                payload_data = new byte[payload_len];
                Array.Copy(recBytes, 8, payload_data, 0, payload_len);

            }
            else if (payload_len == 127)
            {
                Array.Copy(recBytes, 10, masks, 0, 4);
                byte[] uInt64Bytes = new byte[8];
                for (int i = 0; i < 8; i++)
                {
                    uInt64Bytes[i] = recBytes[9 - i];
                }
                UInt64 len = BitConverter.ToUInt64(uInt64Bytes, 0);

                payload_data = new byte[len];
                for (UInt64 i = 0; i < len; i++)
                {
                    payload_data[i] = recBytes[i + 14];
                }
            }
            else
            {
                Array.Copy(recBytes, 2, masks, 0, 4);
                payload_data = new byte[payload_len];
                Array.Copy(recBytes, 6, payload_data, 0, payload_len);

            }

            for (var i = 0; i < payload_len; i++)
            {
                payload_data[i] = (byte)(payload_data[i] ^ masks[i % 4]);
            }

            return Encoding.UTF8.GetString(payload_data);
        }

        /// <summary>
        /// 把发送给客户端消息打包处理（拼接上谁什么时候发的什么消息）
        /// </summary>
        /// <returns>The data.</returns>
        /// <param name="message">Message.</param>
        private byte[] PackageServerData(SocketMessage sm)
        {
            StringBuilder msg = new StringBuilder();
            if (!sm.isLoginMessage)
            { //消息是login信息
                msg.AppendFormat("[{1}]{0}:", sm.Client.Name, sm.Time.ToShortTimeString());
                msg.Append(sm.Message);
            }
            else
            { //处理普通消息
                msg.AppendFormat("[{1}]{0}登陆到服务器", sm.Client.Name, sm.Time.ToShortTimeString());
            }


            byte[] content = null;
            byte[] temp = Encoding.UTF8.GetBytes(msg.ToString());

            if (temp.Length < 126)
            {
                content = new byte[temp.Length + 2];
                content[0] = 0x81;
                content[1] = (byte)temp.Length;
                Array.Copy(temp, 0, content, 2, temp.Length);
            }
            else if (temp.Length < 0xFFFF)
            {
                content = new byte[temp.Length + 4];
                content[0] = 0x81;
                content[1] = 126;
                content[2] = (byte)(temp.Length & 0xFF);
                content[3] = (byte)(temp.Length >> 8 & 0xFF);
                Array.Copy(temp, 0, content, 4, temp.Length);
            }
            else
            {
                // 暂不处理超长内容  
            }

            return content;
        }
    }

    public class ClientInfo
    {
        public byte[] buffer;

        public string NickName { get; set; }

        public EndPoint Id { get; set; }

        public IntPtr handle { get; set; }

        public string Name
        {
            get
            {
                if (!string.IsNullOrEmpty(NickName))
                {
                    return NickName;
                }
                else
                {
                    return string.Format("{0}#{1}", Id, handle);
                }
            }
        }

        public bool IsHandShaked { get; set; }
    }

    public class SocketMessage
    {
        public bool isLoginMessage { get; set; }

        public ClientInfo Client { get; set; }

        public string Message { get; set; }

        public DateTime Time { get; set; }
    }
}