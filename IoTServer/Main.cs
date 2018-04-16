using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using log4net;
using log4net.Config;

namespace IoTServer
{
    public partial class Main : Form
    {

        public Main()
        {
            InitializeComponent();
        }
        private static log4net.ILog log = log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

        private void Main_Load(object sender, EventArgs e)
        {
            Control.CheckForIllegalCrossThreadCalls = false;
        }

        /// <summary>
        /// 显示软件运行日志
        /// </summary>
        /// <param name="info"></param>
        public delegate void ShowLogEventHandler(string info);
        public void ShowLog(string info)
        {
            if (this.ServerLog.InvokeRequired)
            {
                ShowLogEventHandler showLogHandler = new ShowLogEventHandler(this.ShowLog);
                this.ServerLog.BeginInvoke(showLogHandler, info);
            }
            else
            {
                string s = "";
                for (int i = 0; i < this.ServerLog.Lines.Length; i++)
                {
                    if (i > 200)
                        break;
                    s += ("\r\n" + this.ServerLog.Lines[i]);
                }
                this.ServerLog.Text = (DateTime.Now.ToString("yyy-MM-dd HH:mm:ss") + " >>>  " + info + s);
            }
        }





        private void btnStart_Click(object sender, EventArgs e)
        {
            StartListening();
            ShowLog("启动服务...");
            log.Info("启动服务...");
        }



        // State object for reading client data asynchronously  
        public class StateObject
        {
            // Client  socket.  
            public Socket workSocket = null;
            // Size of receive buffer.  
            public const int BufferSize = 1024;
            // Receive buffer.  
            public byte[] buffer = new byte[BufferSize];
            // Received data string.  
            public StringBuilder sb = new StringBuilder();
        }


        // Thread signal.  
        public static ManualResetEvent allDone = new ManualResetEvent(false);

        public static void StartListening()
        {
            // Data buffer for incoming data.  
            byte[] bytes = new Byte[1024];

            // Establish the local endpoint for the socket.  
            // The DNS name of the computer  
            // running the listener is "host.contoso.com".  
            //IPHostEntry ipHostInfo = Dns.GetHostEntry(Dns.GetHostName());
            //IPAddress ipAddress = ipHostInfo.AddressList[0];
            IPAddress ipAddress = IPAddress.Any;

            IPEndPoint localEndPoint = new IPEndPoint(ipAddress, 8088);

            // Create a TCP/IP socket.  
            Socket listener = new Socket(ipAddress.AddressFamily,
                SocketType.Stream, ProtocolType.Tcp);

            // Bind the socket to the local endpoint and listen for incoming connections.  
            try
            {
                listener.Bind(localEndPoint);
                listener.Listen(100);

                while (true)
                {
                    // Set the event to nonsignaled state.  
                    allDone.Reset();

                    // Start an asynchronous socket to listen for connections.  
                    Console.WriteLine("Waiting for a connection...");
                    log.Info("等待TCP客户端连接...");

                    listener.BeginAccept(
                        new AsyncCallback(AcceptCallback),
                        listener);

                    // Wait until a connection is made before continuing.  
                    allDone.WaitOne();
                }

            }
            catch (Exception e)
            {
                Console.WriteLine(e.ToString());
                log.Error("错误信息："+ e.Message);
            }
        }

        public static void AcceptCallback(IAsyncResult ar)
        {
            // Signal the main thread to continue.  
            allDone.Set();

            // Get the socket that handles the client request.  
            Socket listener = (Socket)ar.AsyncState;
            Socket handler = listener.EndAccept(ar);

            // Create the state object.  
            StateObject state = new StateObject();
            state.workSocket = handler;
            handler.BeginReceive(state.buffer, 0, StateObject.BufferSize, 0,
                new AsyncCallback(ReadCallback), state);
        }

        public static void ReadCallback(IAsyncResult ar)
        {
            String content = String.Empty;

            // Retrieve the state object and the handler socket  
            // from the asynchronous state object.  
            StateObject state = (StateObject)ar.AsyncState;
            Socket handler = state.workSocket;

            string workSocketIP =state.workSocket.RemoteEndPoint.ToString();
            // Read data from the client socket.   
            int bytesRead = handler.EndReceive(ar);

            if (bytesRead > 0)
            {
                state.sb.Clear();
                // There  might be more data, so store the data received so far.  
                state.sb.Append(Encoding.ASCII.GetString(
                    state.buffer, 0, bytesRead));

                // Check for end-of-file tag. If it is not there, read   
                // more data.  
                content = state.sb.ToString();
                Console.WriteLine("接收到客户端：{0} 的数据为{1} bytes \n 内容 : {2}", workSocketIP, content.Length, content);
                log.InfoFormat("接收到客户端：{0} 的数据为{1} bytes \n 内容 : {2}", workSocketIP, content.Length, content);

                string SendStr = "Servers at ["+DateTime.Now.ToString("yyy-MM-dd HH:mm:ss")+"] Receive your Data:" +content;

                Send(handler, SendStr);
                log.InfoFormat("发送给客户端：{0} 的数据为{1} bytes \n 内容 : {2}", workSocketIP, SendStr.Length, SendStr);



                handler.BeginReceive(state.buffer, 0, StateObject.BufferSize, 0,
                new AsyncCallback(ReadCallback), state);
               
            }
        }

        private static void Send(Socket handler, String data)
        {
            // Convert the string data to byte data using ASCII encoding.  
            byte[] byteData = Encoding.ASCII.GetBytes(data);

            // Begin sending the data to the remote device.  
            handler.BeginSend(byteData, 0, byteData.Length, 0,
                new AsyncCallback(SendCallback), handler);
        }

        private static void SendCallback(IAsyncResult ar)
        {
            try
            {
                // Retrieve the socket from the state object.  
                Socket handler = (Socket)ar.AsyncState;

                // Complete sending the data to the remote device.  
                int bytesSent = handler.EndSend(ar);
                Console.WriteLine("Sent {0} bytes to client.", bytesSent);

                //handler.Shutdown(SocketShutdown.Both);
                //handler.Close();

            }
            catch (Exception e)
            {
                Console.WriteLine(e.ToString());
            }
        }

        private void btnStop_Click(object sender, EventArgs e)
        {
            //点击"是(YES)"退出程序  
            if (MessageBox.Show("确定要退出程序?", "重要提示",
                        System.Windows.Forms.MessageBoxButtons.YesNo,
                        System.Windows.Forms.MessageBoxIcon.Warning)
                == System.Windows.Forms.DialogResult.Yes)
            {
                notifyIcon1.Visible = false;   //设置图标不可见  
                this.Close();                  //关闭窗体  
                this.Dispose();                //释放资源  
                Application.Exit();            //关闭应用程序窗体  
            }
        }
    }
}
