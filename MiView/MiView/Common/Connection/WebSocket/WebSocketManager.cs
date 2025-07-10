using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.WebSockets;
using System.Text;
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
        }

        /// <summary>
        /// ConstructorWithOpen
        /// </summary>
        /// <param name="HostUrl"></param>
        public WebSocketManager(string HostUrl)
        {
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

        private void OnConnectionLost()
        {
        }


    }
}
