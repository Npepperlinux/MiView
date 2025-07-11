using MiView.Common.Connection.WebSocket.Structures;
using System.Net.WebSockets;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace MiView.Common.Connection.WebSocket
{
    internal class WebSocketTimeLineHome : WebSocketManager
    {
        public static WebSocketTimeLineHome OpenTimeLine(string InstanceURL, string ApiKey)
        {
            // WS取得
            WebSocketTimeLineHome WSTimeLine = new WebSocketTimeLineHome();

            // タイムライン用WebSocket Open
            WSTimeLine.Start(WSTimeLine.GetWSURL(InstanceURL, ApiKey));
            if (WSTimeLine.GetSocketClient() == null)
            {
                throw new InvalidOperationException("connection is not opened.");
            }
            while (WSTimeLine.IsStandBySocketOpen())
            {
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
                while (WSTimeLine.GetSocketState() == WebSocketState.Open)
                {
                    // 受信本体
                    try
                    {
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
                        System.Diagnostics.Debug.WriteLine(ce);
                    }
                }
            });
        }

        private static void DbgOutputSocketReceived(string Response)
        {
            System.Diagnostics.Debug.WriteLine(Response);
        }
    }
}
