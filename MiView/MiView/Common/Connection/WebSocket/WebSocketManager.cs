using MiView.Common.AnalyzeData;
using MiView.Common.Connection.WebSocket.Event;
using MiView.Common.TimeLine;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace MiView.Common.Connection.WebSocket
{
    internal class WebSocketManager
    {
        /// <summary>
        /// Host
        /// </summary>
        private string _HostUrl
        {
            get; set;
        }
        = string.Empty;

        /// <summary>
        /// Status
        /// </summary>
        private WebSocketState _State
        {
            get; set;
        }
        = WebSocketState.None;

        /// <summary>
        /// Status/Command
        /// </summary>
        private WebSocketState _State_Command
        {
            get; set;
        }
        = WebSocketState.None;

        /// <summary>
        /// CloseConnection
        /// </summary>
        private bool _ConnectionClose
        {
            get; set;
        }
        = false;

        /// <summary>
        /// 紐づいているタイムラインオブジェクト
        /// </summary>
        private DataGridTimeLine[]? _TimeLineObject
        {
            get; set;
        }
        = new DataGridTimeLine[0];

        public void SetDataGridTimeLine(DataGridTimeLine timeLine)
        {
            if (this._TimeLineObject == null)
            {
                this._TimeLineObject = new DataGridTimeLine[0];
            }
            this._TimeLineObject = this._TimeLineObject.Concat(new DataGridTimeLine[] { timeLine }).ToArray();
        }

        /// <summary>
        /// WebSocket
        /// </summary>
        private ClientWebSocket _WebSocket
        {
            get; set;
        }
        = new ClientWebSocket();

        public event EventHandler<EventArgs>? ConnectionClosed;

        /// <summary>
        /// Constructor
        /// </summary>
        public WebSocketManager()
        {
            this.ConnectionLost += OnConnectionLost;
            this.DataReceived += OnDataReceived;
        }

        /// <summary>
        /// ConstructorWithOpen
        /// </summary>
        /// <param name="HostUrl"></param>
        public WebSocketManager(string HostUrl)
        {
            this.ConnectionLost += OnConnectionLost;
            this.DataReceived += OnDataReceived;

            this._HostUrl = HostUrl;

            Task.Run(async () =>
            {
                await Watcher();
            });
        }

        /// <summary>
        /// socket open and start
        /// </summary>
        /// <param name="HostUrl"></param>
        /// <returns></returns>
        protected WebSocketManager Start(string HostUrl)
        {
            this._HostUrl = HostUrl;

            Task.Run(async () =>
            {
                await Watcher();
            });

            return this;
        }

        /// <summary>
        /// Prepare for Socket Close
        /// </summary>
        protected void ConnectionAbort()
        {
            this._ConnectionClose = true;
        }

        /// <summary>
        /// Get Socket
        /// </summary>
        /// <returns></returns>
        public ClientWebSocket GetSocketClient()
        {
            return this._WebSocket;
        }

        /// <summary>
        /// Set WebSocket Status
        /// </summary>
        /// <param name="State"></param>
        public void SetSocketState(WebSocketState State)
        {
            this._State = State;
        }

        /// <summary>
        /// Get WebSocket Status
        /// </summary>
        /// <returns></returns>
        public WebSocketState? GetSocketState()
        {
            return this._WebSocket.State;
        }

        /// <summary>
        /// Standby WebSocket Open
        /// </summary>
        /// <returns></returns>
        public bool IsStandBySocketOpen()
        {
            return this.GetSocketState() == WebSocketState.None;
        }

        /// <summary>
        /// SocketWatcher
        /// </summary>
        private async Task Watcher()
        {
            if (_WebSocket == null)
            {
                _WebSocket = new ClientWebSocket();
            }

            while (!this._ConnectionClose)
            {
                Thread.Sleep(1000);

                if (_WebSocket.State != WebSocketState.Open)
                {
                    await CreateAndOpen(this._HostUrl);
                }
            }
        }

        /// <summary>
        /// SocketOpen
        /// </summary>
        /// <param name="HostUrl"></param>
        /// <exception cref="InvalidOperationException"></exception>
        private async Task CreateAndOpen(string HostUrl)
        {
            _HostUrl = HostUrl;

            if ((this._State != WebSocketState.None && 
                 this._State != WebSocketState.Closed))
            {
                throw new InvalidOperationException("Socket is already opened");
            }

            ClientWebSocket? WS = null;
            try
            {
                WS = new ClientWebSocket();
                WS.Options.KeepAliveInterval = TimeSpan.Zero;

                await WS.ConnectAsync(new Uri(this._HostUrl), CancellationToken.None);

                if (WS.State != WebSocketState.Open)
                {
                    throw new InvalidOperationException("connection is not opened.");
                }

                this._WebSocket = WS;
                this._State = WS.State;
            }
            catch (Exception ex)
            {
            }
        }

        protected string GetWSURL(string InstanceURL, string APIKey)
        {
            return string.Format("wss://{0}/streaming?i={1}", InstanceURL, APIKey);
        }

        public event EventHandler<ConnectDataReceivedEventArgs> ConnectionLost;
        protected virtual void OnConnectionLost(object? sender, ConnectDataReceivedEventArgs e)
        {
        }
        protected void CallConnectionLost()
        {
        }

        public event EventHandler<ConnectDataReceivedEventArgs> DataReceived;
        protected virtual void OnDataReceived(object? sender, ConnectDataReceivedEventArgs e)
        {
            if (this._TimeLineObject == null)
            {
                // objectがない場合
                return;
            }
            if (e.MessageRaw == null)
            {
                // データ受信不可能の場合
                return;
            }

            dynamic Res = System.Text.Json.JsonDocument.Parse(e.MessageRaw);
            var t = JsonNode.Parse(e.MessageRaw);

            // ChannelToTimeLineData.Type(t);

            foreach (DataGridTimeLine DGrid in this._TimeLineObject)
            {
                if (DGrid.InvokeRequired)
                {
                    DGrid.Invoke(() => { DGrid.InsertTimeLineData(ChannelToTimeLineContainer.ConvertTimeLineContainer(t)); });
                }
            }
        }
        protected void CallDataReceived(string ResponseMessage)
        {
            DataReceived(this, new ConnectDataReceivedEventArgs() { MessageRaw = ResponseMessage });
        }
    }
}
