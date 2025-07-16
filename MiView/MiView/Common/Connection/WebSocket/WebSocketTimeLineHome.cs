using MiView.Common.AnalyzeData;
using MiView.Common.Connection.WebSocket.Event;
using MiView.Common.Connection.WebSocket.Structures;
using MiView.Common.TimeLine;
using System.Net.WebSockets;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Tasks;

namespace MiView.Common.Connection.WebSocket
{
    internal class WebSocketTimeLineHome : WebSocketManager
    {
        public static WebSocketTimeLineHome OpenTimeLine(string InstanceURL, string? ApiKey)
        {
            // WS取得
            WebSocketTimeLineHome WSTimeLine = new WebSocketTimeLineHome();

            // タイムライン用WebSocket Open
            WSTimeLine.Start(WSTimeLine.GetWSURL(InstanceURL, ApiKey));
            if (WSTimeLine.GetSocketClient() == null)
            {
                throw new InvalidOperationException("connection is not opened.");
            }

            int RetryCnt = 0;
            while (WSTimeLine.IsStandBySocketOpen())
            {
                Thread.Sleep(1000);
                RetryCnt++;
                if (RetryCnt > 10)
                {
                    if (WSTimeLine.GetSocketState() != WebSocketState.Open)
                    {
                        WSTimeLine.OnConnectionLost(WSTimeLine, new EventArgs());
                    }
                }
            }

            // チャンネル接続用
            ConnectMain SendObj = new ConnectMain();
            ConnectMainBody SendBody = new ConnectMainBody() { channel = "homeTimeline", id = "hoge" };
            SendObj.type = "connect";
            SendObj.body = SendBody;

            var SendBytes = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(SendObj));
            var Buffers = new ArraySegment<byte>(SendBytes);

            // ソケットのステータスを一旦リセットする(同じソケット使うので)
            WSTimeLine.SetSocketState(WebSocketState.None);
            Task.Run(async () =>
            {
                // 本チャンのwebsocket接続
                await WSTimeLine.GetSocketClient().SendAsync(Buffers, WebSocketMessageType.Text, true, CancellationToken.None);
            });
            while (WSTimeLine.IsStandBySocketOpen())
            {
            }

            return WSTimeLine;
        }

        public WebSocketTimeLineHome OpenTimeLineDynamic(string InstanceURL, string ApiKey)
        {
            // WS取得
            WebSocketTimeLineHome WSTimeLine = new WebSocketTimeLineHome();

            // タイムライン用WebSocket Open
            this.Start(WSTimeLine.GetWSURL(InstanceURL, ApiKey));
            if (this.GetSocketClient() == null)
            {
                throw new InvalidOperationException("connection is not opened.");
            }
            int RetryCnt = 0;
            while (WSTimeLine.IsStandBySocketOpen())
            {
                Thread.Sleep(1000);
                RetryCnt++;
                if (RetryCnt > 10)
                {
                    if (WSTimeLine.GetSocketState() != WebSocketState.Open)
                    {
                        WSTimeLine.OnConnectionLost(WSTimeLine, new EventArgs());
                    }
                }
            }

            // チャンネル接続用
            ConnectMain SendObj = new ConnectMain();
            ConnectMainBody SendBody = new ConnectMainBody() { channel = "homeTimeline", id = "hoge" };
            SendObj.type = "connect";
            SendObj.body = SendBody;

            var SendBytes = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(SendObj));
            var Buffers = new ArraySegment<byte>(SendBytes);

            // ソケットのステータスを一旦リセットする(同じソケット使うので)
            this.SetSocketState(WebSocketState.None);
            Task.Run(async () =>
            {
                // 本チャンのwebsocket接続
                await this.GetSocketClient().SendAsync(Buffers, WebSocketMessageType.Text, true, CancellationToken.None);
            });
            while (this.IsStandBySocketOpen())
            {
            }

            return this;
        }

        /// <summary>
        /// タイムライン取得
        /// </summary>
        /// <param name="WSTimeLine"></param>
        public static void ReadTimeLineContinuous(WebSocketTimeLineHome WSTimeLine)
        {
            // バッファは多めに取っておく(どうせあとでカットする)
            var ResponseBuffer = new byte[4096 * 4];
            _ = Task.Run(async () =>
            {
                //if (WSTimeLine.GetSocketState() != WebSocketState.Open)
                //{
                //    WSTimeLine.OnConnectionLost(WSTimeLine, new EventArgs());
                //}
                while (WSTimeLine.GetSocketState() == WebSocketState.Open)
                {
                    // 受信本体
                    try
                    {
                        // 受信可能になるまで待機
                        if (WSTimeLine.GetSocketClient().State != WebSocketState.Open)
                        {
                            System.Diagnostics.Debug.WriteLine(WSTimeLine.GetSocketClient().State);
                        }
                        if (WSTimeLine.GetSocketClient().State != WebSocketState.Open && WSTimeLine._HostUrl != null)
                        {
                            // 再接続
                            await WSTimeLine.GetSocketClient().ConnectAsync(new Uri(WSTimeLine._HostUrl), CancellationToken.None);
                        }
                        while (WSTimeLine.GetSocketState() == WebSocketState.Closed)
                        {
                            // 接続スタンバイ
                        }
                        var Response = await WSTimeLine.GetSocketClient().ReceiveAsync(new ArraySegment<byte>(ResponseBuffer), CancellationToken.None);
                        if (Response.MessageType == WebSocketMessageType.Close)
                        {
                            WSTimeLine.ConnectionAbort();
                            return;
                        }
                        else
                        {
                            var ResponseMessage = Encoding.UTF8.GetString(ResponseBuffer, 0, Response.Count);
                            DbgOutputSocketReceived(ResponseMessage);

                            WSTimeLine.CallDataReceived(ResponseMessage);
                        }
                    }
                    catch(Exception ce)
                    {
                        System.Diagnostics.Debug.WriteLine("receive failed");
                        System.Diagnostics.Debug.WriteLine(WSTimeLine._HostUrl);
                        System.Diagnostics.Debug.WriteLine(ce);

                        if (WSTimeLine.GetSocketClient().State != WebSocketState.Open)
                        {
                            Thread.Sleep(1000);

                            WebSocketTimeLineHome.ReadTimeLineContinuous(WSTimeLine);
                        }

                        WSTimeLine.CallConnectionLost();
                    }
                }
            });
        }

        private static void DbgOutputSocketReceived(string Response)
        {
            System.Diagnostics.Debug.WriteLine(Response);
        }

        protected override void OnConnectionLost(object? sender, EventArgs e)
        {
            if (sender == null)
            {
                return;
            }
            if (sender.GetType() != typeof(WebSocketTimeLineHome))
            {
                return;
            }
            // オープンを待つ
            WebSocketTimeLineHome WS = (WebSocketTimeLineHome)sender;
            while (WS.GetSocketState() != WebSocketState.Open)
            {
                // 1分おき
                Thread.Sleep(1000 * 60 * 1);
                System.Diagnostics.Debug.WriteLine("待機中（　＾ω＾）");
                try
                {
                    WS.OpenTimeLineDynamic(this._HostDefinition, this._APIKey);
                }
                catch (Exception)
                {
                }
                System.Diagnostics.Debug.WriteLine("現在の状態：" + ((WebSocketTimeLineHome)sender).GetSocketClient().State);
            }
            if (WS == null)
            {
                // 必ず入ってるはず
                return;
            }

            WebSocketTimeLineHome.ReadTimeLineContinuous(WS);
        }
        protected override void OnDataReceived(object? sender, ConnectDataReceivedEventArgs e)
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
                    try
                    {
                        DGrid.Invoke(() => { DGrid.InsertTimeLineData(ChannelToTimeLineContainer.ConvertTimeLineContainer(this._HostDefinition, t)); });
                    }
                    catch(Exception ce)
                    {
                        System.Diagnostics.Debug.WriteLine(ce.ToString());
                    }
                }
            }
        }
    }
}
