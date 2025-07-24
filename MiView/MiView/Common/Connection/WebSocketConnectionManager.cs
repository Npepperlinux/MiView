using MiView.Common.Connection.WebSocket.Misskey.v2025;
using MiView.Common.TimeLine;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.WebSockets;
using System.Threading;
using System.Threading.Tasks;

namespace MiView.Common.Connection
{
    /// <summary>
    /// WebSocket接続の管理を行うクラス
    /// </summary>
    public class WebSocketConnectionManager
    {
        // インスタンス → タイムライン種別 → WebSocket接続
        private Dictionary<string, Dictionary<string, WebSocketTimeLineCommon>> _persistentConnections = new();
        private Dictionary<string, string> _instanceTokens = new();
        private List<WebSocketTimeLineCommon> _unifiedTimelineConnections = new();
        private Timer? _reconnectTimer;
        private const int RECONNECT_INTERVAL_MINUTES = 2; // 2分間隔に変更
        private SemaphoreSlim _connectionSemaphore = new SemaphoreSlim(10, 10); // 最大10個同時接続
        
        // **MEMORY LEAK FIX: Add size limits for collections**
        private const int MAX_PERSISTENT_CONNECTIONS = 50; // 最大50インスタンス
        private const int MAX_UNIFIED_CONNECTIONS = 20; // 統合TL接続最大20
        private const int MAX_INACTIVE_HOURS = 2; // 2時間非アクティブで削除
        private DateTime _lastCleanupTime = DateTime.Now;

        public event EventHandler<TimeLineDataReceivedEventArgs>? TimeLineDataReceived;
        public event EventHandler<ConnectionStatusChangedEventArgs>? ConnectionStatusChanged;

        public WebSocketConnectionManager()
        {
            StartReconnectTimer();
        }

        /// <summary>
        /// インスタンスの持続接続を開始
        /// </summary>
        public async Task ConnectPersistentInstance(string instanceName, string? apiKey = null)
        {
            // WebSocket接続用にプロトコルを除去（GetWSURLでwss://が追加される）
            var normalizedInstanceName = instanceName;
            if (instanceName.StartsWith("http://"))
            {
                normalizedInstanceName = instanceName.Substring(7);
            }
            else if (instanceName.StartsWith("https://"))
            {
                normalizedInstanceName = instanceName.Substring(8);
            }
            
            Console.WriteLine($"ConnectPersistentInstance called for: {instanceName} (normalized: {normalizedInstanceName})");
            Console.WriteLine($"API key provided: {!string.IsNullOrEmpty(apiKey)}");
            if (!string.IsNullOrEmpty(apiKey))
            {
                Console.WriteLine($"API key length: {apiKey.Length}");
                Console.WriteLine($"API key preview: [HIDDEN]");
            }
            
            if (!string.IsNullOrEmpty(apiKey))
            {
                _instanceTokens[instanceName] = apiKey;
                Console.WriteLine($"API key set for: {instanceName}");
            }
            else
            {
                Console.WriteLine($"No API key provided for: {instanceName}");
            }

            if (!_persistentConnections.ContainsKey(instanceName))
            {
                _persistentConnections[instanceName] = new Dictionary<string, WebSocketTimeLineCommon>();
                Console.WriteLine($"Created connection dictionary for: {instanceName}");
            }

            // 全てのタイムライン種別を持続接続で管理
            var timelineTypes = new[]
            {
                ("ローカルTL", WebSocketTimeLineCommon.ConnectTimeLineKind.Local),
                ("ソーシャルTL", WebSocketTimeLineCommon.ConnectTimeLineKind.Social),
                ("グローバルTL", WebSocketTimeLineCommon.ConnectTimeLineKind.Global),
                ("ホームTL", WebSocketTimeLineCommon.ConnectTimeLineKind.Home)
            };
            
            // 独自タイムラインの検出と追加（一時的に無効化）
            // var customTimelineTypes = await DetectCustomTimelines(instanceName, apiKey);
            // if (customTimelineTypes.Any())
            // {
            //     Console.WriteLine($"Detected custom timelines for {instanceName}: {string.Join(", ", customTimelineTypes.Select(t => t.Item1))}");
            //     timelineTypes = timelineTypes.Concat(customTimelineTypes).ToArray();
            // }

            Console.WriteLine($"Attempting to connect {timelineTypes.Length} timeline types for: {instanceName}");

            // 全てのタイムライン種別を同時に接続（並列度制限付き）
            var connectionTasks = new List<Task<bool>>();
            var semaphore = new SemaphoreSlim(4, 4); // 1インスタンスあたり最大4つの同時接続
            
            foreach (var (timelineType, kind) in timelineTypes)
            {
                var task = Task.Run(async () =>
                {
                    await semaphore.WaitAsync();
                    try
                    {
                        Console.WriteLine($"Starting connection to {timelineType} for {instanceName}");
                        var success = await ConnectTimelineType(normalizedInstanceName, timelineType, kind, apiKey);
                        Console.WriteLine($"Connection to {timelineType} for {instanceName}: {(success ? "SUCCESS" : "FAILED")}");
                        return success;
                    }
                    finally
                    {
                        semaphore.Release();
                    }
                });
                connectionTasks.Add(task);
            }

            // 全ての接続タスクを同時実行
            var results = await Task.WhenAll(connectionTasks);
            int successfulConnections = results.Count(x => x);

            Console.WriteLine($"All timeline connections completed for: {instanceName} ({successfulConnections}/{timelineTypes.Length} successful)");
            
            // 失敗した接続の詳細を表示
            for (int i = 0; i < results.Length; i++)
            {
                if (!results[i])
                {
                    Console.WriteLine($"Failed connection: {instanceName} - {timelineTypes[i].Item1}");
                }
                else
                {
                    Console.WriteLine($"Successful connection: {instanceName} - {timelineTypes[i].Item1}");
                }
            }
            
            // 少なくとも1つの接続が成功した場合のみConnectedイベントを発火
            if (successfulConnections > 0)
            {
                Console.WriteLine($"Firing Connected event for {instanceName} with {successfulConnections} successful connections");
                OnConnectionStatusChanged(instanceName, "Connected");
            }
            else
            {
                Console.WriteLine($"No successful connections for {instanceName}, not firing Connected event");
            }
        }

        /// <summary>
        /// 特定のタイムライン種別に接続
        /// </summary>
        private async Task<bool> ConnectTimelineType(string instanceName, string timelineType, 
            WebSocketTimeLineCommon.ConnectTimeLineKind kind, string? apiKey)
        {
            // セマフォで同時接続数を制限
            await _connectionSemaphore.WaitAsync();
            try
            {
                Console.WriteLine($"Creating connection instance for {instanceName} - {timelineType}");
                var connection = WebSocketTimeLineCommon.CreateInstance(kind);
                if (connection != null)
                {
                    Console.WriteLine($"Connection instance created successfully for {instanceName} - {timelineType}");
                    
                    connection.TimeLineDataReceived += (sender, container) =>
                    {
                        Console.WriteLine($"Timeline data received from {instanceName} - {timelineType}");
                        OnTimeLineDataReceived(instanceName, timelineType, container);
                    };

                    // 接続を試行（最大3回）
                    var maxRetries = 3;
                    var retryDelaySeconds = 5;
                    
                    for (int retry = 1; retry <= maxRetries; retry++)
                    {
                        try
                        {
                            Console.WriteLine($"Attempting connection {retry}/{maxRetries} for {instanceName} - {timelineType}");
                            
                            // 接続タイムアウトを30秒に設定
                            var timeoutTask = Task.Delay(TimeSpan.FromSeconds(30));
                            
                            var connectionTask = Task.Run(() =>
                            {
                                try
                                {
                                    Console.WriteLine($"Opening timeline for {instanceName} - {timelineType}");
                                    connection.OpenTimeLine(instanceName, apiKey);
                                    Console.WriteLine($"Timeline opened successfully for {instanceName} - {timelineType}");
                                    
                                    Console.WriteLine($"Starting continuous reading for {instanceName} - {timelineType}");
                                    WebSocketTimeLineCommon.ReadTimeLineContinuous(connection);
                                    Console.WriteLine($"Continuous reading started for {instanceName} - {timelineType}");

                                    _persistentConnections[instanceName][timelineType] = connection;
                                    Console.WriteLine($"Persistent connection established: {instanceName} - {timelineType}");
                                    return true;
                                }
                                catch (Exception ex)
                                {
                                    Console.WriteLine($"Failed to connect {instanceName} - {timelineType} (attempt {retry}): {ex.Message}");
                                    return false;
                                }
                            });
                            
                            // タイムアウトまたは接続完了を待つ
                            var completedTask = await Task.WhenAny(connectionTask, timeoutTask);
                            if (completedTask == timeoutTask)
                            {
                                Console.WriteLine($"Connection timeout for {instanceName} - {timelineType} (attempt {retry})");
                                continue;
                            }
                            
                            // 接続タスクの完了を待つ
                            var connectionSuccess = await connectionTask;
                            if (connectionSuccess)
                            {
                                Console.WriteLine($"Connection successful for {instanceName} - {timelineType} on attempt {retry}");
                                return true;
                            }
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"Connection attempt {retry} failed for {instanceName} - {timelineType}: {ex.Message}");
                        }
                        
                        // 最後の試行でない場合は待機
                        if (retry < maxRetries)
                        {
                            var delaySeconds = retryDelaySeconds * retry; // 指数バックオフ
                            Console.WriteLine($"Waiting {delaySeconds} seconds before retry {retry + 1}");
                            await Task.Delay(TimeSpan.FromSeconds(delaySeconds));
                        }
                    }
                    
                    Console.WriteLine($"All connection attempts failed for {instanceName} - {timelineType}");
                    return false;
                }
                else
                {
                    Console.WriteLine($"Failed to create connection instance for {instanceName} - {timelineType}");
                    return false;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error creating connection for {instanceName} - {timelineType}: {ex.Message}");
                return false;
            }
            finally
            {
                _connectionSemaphore.Release();
            }
        }

        /// <summary>
        /// 統合TL用の接続を取得
        /// </summary>
        public List<object> GetUnifiedTimelineConnections()
        {
            _unifiedTimelineConnections.Clear();

            foreach (var instance in _persistentConnections.Keys)
            {
                if (_persistentConnections[instance].ContainsKey("ソーシャルTL"))
                {
                    var socialConnection = _persistentConnections[instance]["ソーシャルTL"];
                    _unifiedTimelineConnections.Add(socialConnection);
                }
            }

            return _unifiedTimelineConnections.Cast<object>().ToList();
        }

        /// <summary>
        /// 特定のインスタンスの特定のタイムライン接続を取得
        /// </summary>
        public object? GetConnection(string instanceName, string timelineType)
        {
            if (_persistentConnections.ContainsKey(instanceName) &&
                _persistentConnections[instanceName].ContainsKey(timelineType))
            {
                return _persistentConnections[instanceName][timelineType];
            }
            return null;
        }

        /// <summary>
        /// 接続状態をデバッグ出力
        /// </summary>
        public void DebugConnectionStatus()
        {
            Console.WriteLine("=== Connection Status Debug ===");
            Console.WriteLine($"Total instances: {_persistentConnections.Count}");
            
            foreach (var instance in _persistentConnections.Keys)
            {
                Console.WriteLine($"Instance: {instance}");
                var connections = _persistentConnections[instance];
                Console.WriteLine($"  Timeline connections: {connections.Count}");
                
                foreach (var timelineType in connections.Keys)
                {
                    var connection = connections[timelineType];
                    var socket = connection.GetSocketClient();
                    var state = socket?.State.ToString() ?? "Unknown";
                    Console.WriteLine($"    {timelineType}: {state}");
                }
            }
            Console.WriteLine("=== End Connection Status Debug ===");
        }

        /// <summary>
        /// インスタンスの接続を切断
        /// </summary>
        public async Task DisconnectInstance(string instanceName, bool isUserInitiated = true)
        {
            Console.WriteLine($"WebSocketConnectionManager.DisconnectInstance called for: {instanceName}, isUserInitiated: {isUserInitiated}");
            Console.WriteLine($"Available instances: {string.Join(", ", _persistentConnections.Keys)}");
            
            if (_persistentConnections.ContainsKey(instanceName))
            {
                Console.WriteLine($"Found instance {instanceName}, disconnecting {_persistentConnections[instanceName].Count} connections");
                var instanceConnections = _persistentConnections[instanceName];
                foreach (var connection in instanceConnections.Values)
                {
                    Console.WriteLine($"Disconnecting connection: {connection}");
                    await DisconnectConnection(connection, isUserInitiated);
                    _unifiedTimelineConnections.Remove(connection);
                }
                _persistentConnections.Remove(instanceName);
                Console.WriteLine($"Removed instance {instanceName} from persistent connections");
                OnConnectionStatusChanged(instanceName, "Disconnected");
            }
            else
            {
                Console.WriteLine($"Instance {instanceName} not found in persistent connections");
            }
        }

        /// <summary>
        /// 全ての接続を切断
        /// </summary>
        public async Task DisconnectAll(bool isUserInitiated = true)
        {
            foreach (var instanceName in _persistentConnections.Keys.ToList())
            {
                await DisconnectInstance(instanceName, isUserInitiated);
            }
            _persistentConnections.Clear();
            _unifiedTimelineConnections.Clear();
        }

        /// <summary>
        /// 個別の接続を切断
        /// </summary>
        private async Task DisconnectConnection(WebSocketTimeLineCommon connection, bool isUserInitiated = true)
        {
            try
            {
                Console.WriteLine($"DisconnectConnection called, isUserInitiated: {isUserInitiated}");
                
                // ユーザー操作による切断かどうかを設定
                connection.SetUserInitiatedDisconnect(isUserInitiated);
                Console.WriteLine("Set user initiated disconnect flag");
                
                var socket = connection.GetSocketClient();
                Console.WriteLine($"Socket state: {socket?.State}");
                
                if (socket != null && socket.State == WebSocketState.Open)
                {
                    Console.WriteLine("Closing WebSocket connection");
                    await socket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Disconnect", CancellationToken.None);
                    Console.WriteLine("WebSocket connection closed successfully");
                }
                else
                {
                    Console.WriteLine("Socket is null or not open, skipping close");
                }
                
                // **MEMORY LEAK FIX: Dispose the connection to clean up resources and event handlers**
                connection?.Dispose();
                Console.WriteLine("Connection disposed successfully");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error disconnecting connection: {ex.Message}");
                Console.WriteLine($"Exception details: {ex}");
            }
        }

        /// <summary>
        /// 接続状態をチェックして再接続
        /// </summary>
        private void StartReconnectTimer()
        {
            _reconnectTimer = new Timer(CheckAndReconnect, null, 
                TimeSpan.FromMinutes(RECONNECT_INTERVAL_MINUTES), 
                TimeSpan.FromMinutes(RECONNECT_INTERVAL_MINUTES));
        }

        /// <summary>
        /// **MEMORY LEAK FIX: Cleanup inactive connections and enforce size limits**
        /// </summary>
        public void CleanupInactiveConnections()
        {
            try
            {
                var now = DateTime.Now;
                var cutoffTime = now.AddHours(-MAX_INACTIVE_HOURS);
                
                // サイズ制限チェック - インスタンス数制限
                if (_persistentConnections.Count > MAX_PERSISTENT_CONNECTIONS)
                {
                    Console.WriteLine($"Cleaning up connections: {_persistentConnections.Count} > {MAX_PERSISTENT_CONNECTIONS}");
                    
                    // 最も古い接続から削除
                    var instancesToRemove = _persistentConnections.Keys
                        .Take(_persistentConnections.Count - MAX_PERSISTENT_CONNECTIONS)
                        .ToList();
                    
                    foreach (var instance in instancesToRemove)
                    {
                        if (_persistentConnections.ContainsKey(instance))
                        {
                            foreach (var connection in _persistentConnections[instance].Values)
                            {
                                connection?.Dispose();
                            }
                            _persistentConnections.Remove(instance);
                            _instanceTokens.Remove(instance);
                            Console.WriteLine($"Removed inactive instance: {instance}");
                        }
                    }
                }
                
                // 統合タイムライン接続数制限
                if (_unifiedTimelineConnections.Count > MAX_UNIFIED_CONNECTIONS)
                {
                    var connectionsToRemove = _unifiedTimelineConnections.Count - MAX_UNIFIED_CONNECTIONS;
                    for (int i = 0; i < connectionsToRemove; i++)
                    {
                        _unifiedTimelineConnections[i]?.Dispose();
                        _unifiedTimelineConnections.RemoveAt(i);
                    }
                    Console.WriteLine($"Cleaned up {connectionsToRemove} unified timeline connections");
                }
                
                _lastCleanupTime = now;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error during connection cleanup: {ex.Message}");
            }
        }

        private void CheckAndReconnect(object? state)
        {
            // **MEMORY LEAK FIX: Run cleanup before reconnection check**
            if ((DateTime.Now - _lastCleanupTime).TotalMinutes > 30) // 30分に1回クリーンアップ
            {
                CleanupInactiveConnections();
            }
            
            Task.Run(async () =>
            {
                Console.WriteLine("=== Starting connection health check ===");
                
                foreach (var instanceName in _persistentConnections.Keys.ToList())
                {
                    if (_persistentConnections.ContainsKey(instanceName))
                    {
                        var connectionsToReconnect = new List<string>();

                        foreach (var kvp in _persistentConnections[instanceName].ToList())
                        {
                            var timelineType = kvp.Key;
                            var connection = kvp.Value;

                            // 接続状態をチェック
                            var isAlive = IsConnectionAlive(connection);
                            var isUserInitiated = connection.IsUserInitiatedDisconnect();
                            
                            Console.WriteLine($"Connection check: {instanceName} - {timelineType}, Alive: {isAlive}, UserInitiated: {isUserInitiated}");

                            // ユーザー操作による切断でない場合のみ再接続
                            if (!isAlive && !isUserInitiated)
                            {
                                connectionsToReconnect.Add(timelineType);
                                Console.WriteLine($"Connection lost, will reconnect: {instanceName} - {timelineType}");
                            }
                        }

                        // 再接続を実行
                        foreach (var timelineType in connectionsToReconnect)
                        {
                            try
                            {
                                Console.WriteLine($"Attempting reconnection: {instanceName} - {timelineType}");
                                await ReconnectTimeline(instanceName, timelineType);
                                Console.WriteLine($"Reconnection successful: {instanceName} - {timelineType}");
                            }
                            catch (Exception ex)
                            {
                                Console.WriteLine($"Reconnection failed: {instanceName} - {timelineType}: {ex.Message}");
                            }
                        }
                    }
                }
                
                Console.WriteLine("=== Connection health check completed ===");
            });
        }

        private bool IsConnectionAlive(WebSocketTimeLineCommon connection)
        {
            try
            {
                if (connection == null)
                    return false;
                
                var socket = connection.GetSocketClient();
                if (socket == null)
                    return false;
                
                var state = socket.State;
                Console.WriteLine($"Connection state check: {state}");
                
                return state == WebSocketState.Open;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error checking connection state: {ex.Message}");
                return false;
            }
        }

        private async Task ReconnectTimeline(string instanceName, string timelineType)
        {
            var kind = timelineType switch
            {
                "ローカルTL" => WebSocketTimeLineCommon.ConnectTimeLineKind.Local,
                "ソーシャルTL" => WebSocketTimeLineCommon.ConnectTimeLineKind.Social,
                "グローバルTL" => WebSocketTimeLineCommon.ConnectTimeLineKind.Global,
                "ホームTL" => WebSocketTimeLineCommon.ConnectTimeLineKind.Home,
                _ => WebSocketTimeLineCommon.ConnectTimeLineKind.Local
            };

            var apiKey = _instanceTokens.ContainsKey(instanceName) ? _instanceTokens[instanceName] : null;
            await ConnectTimelineType(instanceName, timelineType, kind, apiKey);
        }

        private void OnTimeLineDataReceived(string instanceName, string timelineType, TimeLineContainer container)
        {
            // SOURCEフィールドにサーバー名を設定
            container.SOURCE = instanceName;
            
            TimeLineDataReceived?.Invoke(this, new TimeLineDataReceivedEventArgs
            {
                InstanceName = instanceName,
                TimelineType = timelineType,
                Container = container
            });
        }

        private void OnConnectionStatusChanged(string instanceName, string status)
        {
            ConnectionStatusChanged?.Invoke(this, new ConnectionStatusChangedEventArgs
            {
                InstanceName = instanceName,
                Status = status
            });
        }
        
        /// <summary>
        /// 独自タイムラインを検出（無効化）
        /// </summary>
        private Task<List<(string, WebSocketTimeLineCommon.ConnectTimeLineKind)>> DetectCustomTimelines(string instanceName, string? apiKey)
        {
            // 独自TLは無視するため、空のリストを返す
            Console.WriteLine($"Custom timeline detection disabled for {instanceName}");
            return Task.FromResult(new List<(string, WebSocketTimeLineCommon.ConnectTimeLineKind)>());
        }

        public void Dispose()
        {
            _reconnectTimer?.Dispose();
            _connectionSemaphore?.Dispose();
            // 非同期で切断処理を実行（UIを待たせない）
            Task.Run(async () =>
            {
                try
                {
                    await DisconnectAll();
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error during Dispose: {ex.Message}");
                }
            });
        }
    }

    public class TimeLineDataReceivedEventArgs : EventArgs
    {
        public string InstanceName { get; set; } = string.Empty;
        public string TimelineType { get; set; } = string.Empty;
        public TimeLineContainer Container { get; set; } = new();
    }

    public class ConnectionStatusChangedEventArgs : EventArgs
    {
        public string InstanceName { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
    }
}