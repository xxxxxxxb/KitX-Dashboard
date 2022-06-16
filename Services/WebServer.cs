﻿using KitX_Dashboard.Data;
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;

#pragma warning disable CS8600 // 将 null 字面量或可能为 null 的值转换为非 null 类型。
#pragma warning disable CS8602 // 解引用可能出现空引用。

namespace KitX_Dashboard.Services
{
    public class WebServer : IDisposable
    {
        public WebServer()
        {
            listener = new(IPAddress.Any, 0);
            listener.Start();
            int port = ((IPEndPoint)listener.LocalEndpoint).Port;
            GlobalInfo.ServerPortNumber = port;
            Program.LocalLogger.Log("Logger_Debug", $"Server Port: {port}",
                BasicHelper.LiteLogger.LoggerManager.LogLevel.Debug);

            acceptClientThread = new(AcceptClient);
            acceptClientThread.Start();
        }

        /// <summary>
        /// 停止进程
        /// </summary>
        public void Stop()
        {
            keepListen = false;

            foreach (KeyValuePair<string, TcpClient> item in clients)
            {
                item.Value.Close();
                item.Value.Dispose();
            }

            acceptClientThread.Join();
        }

        public Thread acceptClientThread;
        public TcpListener listener;
        public bool keepListen = true;

        public readonly Dictionary<string, TcpClient> clients = new();

        /// <summary>
        /// 接收客户端
        /// </summary>
        private void AcceptClient()
        {
            try
            {
                while (keepListen)
                {
                    if (listener.Pending())
                    {
                        TcpClient client = listener.AcceptTcpClient();
                        IPEndPoint endpoint = client.Client.RemoteEndPoint as IPEndPoint;
                        clients.Add(endpoint.ToString(), client);

                        Program.LocalLogger.Log("Logger_Debug", $"New connection: {endpoint}",
                            BasicHelper.LiteLogger.LoggerManager.LogLevel.Debug);

                        //接收消息线程
                        new Thread(() =>
                        {
                            ReciveMessage(client);
                        }).Start();
                    }
                    else
                    {
                        Thread.Sleep(1000);
                    }
                }
            }
            catch (Exception ex)
            {
                Program.LocalLogger.Log("Logger_Debug", $"Error: {ex.Message}",
                    BasicHelper.LiteLogger.LoggerManager.LogLevel.Error);
            }
        }

        /// <summary>
        /// 接收消息
        /// </summary>
        /// <param name="obj">TcpClient</param>
        private void ReciveMessage(object obj)
        {
            TcpClient client = obj as TcpClient;
            IPEndPoint endpoint = null;
            NetworkStream stream = null;

            try
            {
                endpoint = client.Client.RemoteEndPoint as IPEndPoint;
                stream = client.GetStream();

                while (keepListen)
                {
                    byte[] data = new byte[1024];
                    //如果远程主机已关闭连接,Read将立即返回零字节
                    int length = stream.Read(data, 0, data.Length);
                    if (length > 0)
                    {
                        #region if
                        string msg = Encoding.UTF8.GetString(data, 0, length);

                        Console.WriteLine(string.Format("{0}:{1}", endpoint.ToString(), msg));

                        //发送到其他客户端
                        //foreach (KeyValuePair<string, TcpClient> kvp in clients)
                        //{
                        //    if (kvp.Value != client)
                        //    {
                        //        byte[] writeData = Encoding.UTF8.GetBytes(msg);
                        //        NetworkStream writeStream = kvp.Value.GetStream();
                        //        writeStream.Write(writeData, 0, writeData.Length);
                        //    }
                        //}
                        #endregion
                    }
                    else
                    {
                        //客户端断开连接 跳出循环
                        break;
                    }
                }
            }
            catch (Exception ex)
            {
                Program.LocalLogger.Log("Logger_Debug", $"Error: {ex.Message}",
                    BasicHelper.LiteLogger.LoggerManager.LogLevel.Error);
                //Read是阻塞方法 客户端退出是会引发异常 释放资源 结束此线程
            }
            finally
            {
                //释放资源
                stream.Close();
                stream.Dispose();
                clients.Remove(endpoint.ToString());
                client.Dispose();
            }
        }

        public void Dispose()
        {
            keepListen = false;
            listener.Stop();
            acceptClientThread.Join();
            GC.SuppressFinalize(this);
        }
    }
}

#pragma warning restore CS8602 // 解引用可能出现空引用。
#pragma warning restore CS8600 // 将 null 字面量或可能为 null 的值转换为非 null 类型。
