using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.Controls.Selection;
using System;
using System.Collections.ObjectModel;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using System.IO;
using System.Linq;
using MiView.Common.TimeLine;
using MiView.Common.AnalyzeData.Format;
using MiView.Common.Connection.WebSocket.Misskey.v2025;
using MiView.Common.AnalyzeData;

namespace MiView
{
    public partial class MainWindow : Avalonia.Controls.Window
    {
        private ClientWebSocket? _webSocket;
        private CancellationTokenSource? _cancellationTokenSource;
        private ObservableCollection<string> _instances = new();
        private Dictionary<string, List<string>> _serverTabs = new();
        private Dictionary<string, string> _instanceTokens = new();
        private const string SETTINGS_FILE = "settings.json";
        private int _selectedTabIndex = 0;
        private int _noteCount = 0;
        private List<TimeLineContainer> _timelineItems = new();
        private WebSocketTimeLineCommon? _webSocketTimeLine;
        private Dictionary<string, List<TimeLineContainer>> _timelineCache = new();
        private const int MAX_CACHED_ITEMS = 1000;
        private List<WebSocketTimeLineCommon> _unifiedTimelineConnections = new();

        public MainWindow()
        {
            InitializeComponent();
            InitializeUI();
        }

        private void InitializeUI()
        {
            // インスタンス選択コンボボックスの初期化
            cmbInstanceSelect.ItemsSource = _instances;
            cmbInstanceSelect.SelectionChanged += OnInstanceSelectionChanged;
            
            // 初期メッセージ
            tsLabelMain.Text = "インスタンスを選択して「接続」ボタンを押してください";
            tsLabelNoteCount.Text = "0/0";
            
            // 設定を読み込み
            LoadSettings();
            
            // テスト用の投稿を追加
            AddTestTimelineItems();
        }

        private void AddTestTimelineItems()
        {
            // ローディングメッセージを削除
            timelineContainer.Children.Clear();
            
            // テスト用のタイムライン項目を追加
            var testItems = new[]
            {
                ("テスト投稿 1: MiViewのテストです。LinuxでAvalonia UIが動作中。", "homeTimeline", "mi.ruruke.moe"),
                ("テスト投稿 2: 元のWindows FormsデザインをAvaloniaで再現。", "localTimeline", "mi.ruruke.moe"),
                ("テスト投稿 3: クロスプラットフォーム対応完了。", "socialTimeline", "mi.ruruke.moe")
            };

            for (int i = 0; i < testItems.Length; i++)
            {
                var (content, channel, instance) = testItems[i];
                
                // TimeLineContainerを作成
                var timelineItem = new TimeLineContainer
                {
                    USERID = $"user{i + 1}",
                    USERNAME = $"テストユーザー{i + 1}",
                    TLFROM = channel,
                    RENOTED = false,
                    REPLAYED = false,
                    PROTECTED = TimeLineContainer.PROTECTED_STATUS.Public,
                    ORIGINAL = JsonNode.Parse($"{{\"text\":\"{content}\",\"createdAt\":\"{DateTime.Now.AddMinutes(-i):yyyy-MM-ddTHH:mm:ss.fffZ}\",\"user\":{{\"username\":\"{$"テストユーザー{i + 1}"}\"}}}}")!,
                    DETAIL = content,
                    UPDATEDAT = DateTime.Now.AddMinutes(-i).ToString("yyyy-MM-dd HH:mm:ss.fff"),
                    SOURCE = instance
                };
                
                AddTimelineItem(timelineItem, instance);
            }
            
        }

        private void AddTimelineItem(TimeLineContainer timelineItem, string instance = "misskey.io")
        {
            // TimeLineContainerからNoteオブジェクトを作成
            var note = new Note { Node = timelineItem.ORIGINAL };
            
            // 交互の行色を決定
            var isEvenRow = (_noteCount % 2 == 0);
            var backgroundColor = isEvenRow ? Avalonia.Media.Brushes.White : Avalonia.Media.Brush.Parse("#F5F5F5");
            
            var timelineGrid = new Grid
            {
                Background = backgroundColor,
                Height = 18,
                Cursor = new Avalonia.Input.Cursor(Avalonia.Input.StandardCursorType.Hand),
                ColumnDefinitions =
                {
                    new Avalonia.Controls.ColumnDefinition { Width = new Avalonia.Controls.GridLength(20) }, // Icon1
                    new Avalonia.Controls.ColumnDefinition { Width = new Avalonia.Controls.GridLength(20) }, // Icon2
                    new Avalonia.Controls.ColumnDefinition { Width = new Avalonia.Controls.GridLength(80) }, // User
                    new Avalonia.Controls.ColumnDefinition { Width = new Avalonia.Controls.GridLength(1, Avalonia.Controls.GridUnitType.Star) }, // Content
                    new Avalonia.Controls.ColumnDefinition { Width = new Avalonia.Controls.GridLength(150) }, // Timestamp
                    new Avalonia.Controls.ColumnDefinition { Width = new Avalonia.Controls.GridLength(120) } // Instance
                }
            };

            // ホバー効果を追加
            timelineGrid.PointerEntered += (sender, e) =>
            {
                timelineGrid.Background = Avalonia.Media.Brush.Parse("#E8F4FD");
            };
            
            timelineGrid.PointerExited += (sender, e) =>
            {
                timelineGrid.Background = backgroundColor;
            };

            // 各列にBorderとTextBlockを追加
            for (int i = 0; i < 6; i++)
            {
                var border = new Border
                {
                    BorderBrush = Avalonia.Media.Brush.Parse("#8C8C8C"),
                    BorderThickness = new Avalonia.Thickness(0, 0, i < 5 ? 1 : 0, 1),
                    [Grid.ColumnProperty] = i
                };

                var textBlock = new TextBlock
                {
                    FontSize = 11,
                    Foreground = Avalonia.Media.Brushes.Black,
                    VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center,
                    Margin = new Avalonia.Thickness(2, 0),
                    TextTrimming = Avalonia.Media.TextTrimming.CharacterEllipsis
                };

                switch (i)
                {
                    case 0:
                        // Status icon based on timeline item properties
                        if (timelineItem.RENOTED)
                        {
                            textBlock.Text = "🔄";
                            textBlock.Foreground = Avalonia.Media.Brushes.Green;
                        }
                        else if (timelineItem.REPLAYED)
                        {
                            textBlock.Text = "💬";
                        }
                        else
                        {
                            textBlock.Text = "🟢";
                        }
                        textBlock.FontSize = 8;
                        textBlock.HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center;
                        break;
                    case 1:
                        // Protection status icon
                        textBlock.Text = timelineItem.PROTECTED switch
                        {
                            TimeLineContainer.PROTECTED_STATUS.Direct => "🔒",
                            TimeLineContainer.PROTECTED_STATUS.Follower => "👥",
                            TimeLineContainer.PROTECTED_STATUS.Home => "🏠",
                            _ => "🔵"
                        };
                        textBlock.FontSize = 8;
                        textBlock.HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center;
                        break;
                    case 2:
                        textBlock.Text = timelineItem.USERNAME;
                        textBlock.FontWeight = Avalonia.Media.FontWeight.Bold;
                        break;
                    case 3:
                        textBlock.Text = timelineItem.DETAIL;
                        textBlock.TextWrapping = Avalonia.Media.TextWrapping.Wrap;
                        break;
                    case 4:
                        textBlock.Text = timelineItem.UPDATEDAT;
                        textBlock.FontSize = 10;
                        textBlock.Foreground = Avalonia.Media.Brush.Parse("#666666");
                        break;
                    case 5:
                        textBlock.Text = timelineItem.SOURCE;
                        textBlock.FontSize = 10;
                        textBlock.Foreground = Avalonia.Media.Brush.Parse("#666666");
                        break;
                }

                border.Child = textBlock;
                timelineGrid.Children.Add(border);
            }

            // クリックイベントを追加
            timelineGrid.PointerPressed += (sender, e) =>
            {
                SetTimelineDetails(timelineItem, note);
            };

            // タイムラインの先頭に追加
            timelineContainer.Children.Insert(0, timelineGrid);
            
            // リストに追加
            _timelineItems.Add(timelineItem);
            
            // 投稿数をカウント
            _noteCount++;
            tsLabelNoteCount.Text = $"{_noteCount}/9999";
        }

        private Button CreateActionButton(string emoji, string action)
        {
            var button = new Button
            {
                Content = emoji,
                FontSize = 14,
                Background = Avalonia.Media.Brushes.Transparent,
                BorderThickness = new Avalonia.Thickness(0),
                Padding = new Avalonia.Thickness(8, 4),
                Cursor = new Avalonia.Input.Cursor(Avalonia.Input.StandardCursorType.Hand)
            };
            
            Avalonia.Controls.ToolTip.SetTip(button, action);
            return button;
        }

        private string GetRelativeTime(DateTime timestamp)
        {
            var now = DateTime.Now;
            var diff = now - timestamp;

            if (diff.TotalMinutes < 1)
                return "now";
            if (diff.TotalMinutes < 60)
                return $"{(int)diff.TotalMinutes}m";
            if (diff.TotalHours < 24)
                return $"{(int)diff.TotalHours}h";
            if (diff.TotalDays < 7)
                return $"{(int)diff.TotalDays}d";
            
            return timestamp.ToString("MM/dd");
        }

        private async void cmdConnect_Click(object? sender, RoutedEventArgs e)
        {
            Console.WriteLine("Connect button clicked!");
            var instanceUrl = cmbInstanceSelect.SelectedItem?.ToString()?.Trim();
            Console.WriteLine($"Selected instance: {instanceUrl}");
            
            if (string.IsNullOrEmpty(instanceUrl))
            {
                Console.WriteLine("No instance selected, showing add dialog");
                // 新しいインスタンスを追加するためのダイアログを表示
                await ShowAddInstanceDialog();
                return;
            }
            
            // 既存のインスタンスの場合は接続
            var apiKey = _instanceTokens.ContainsKey(instanceUrl) ? _instanceTokens[instanceUrl] : null;
            Console.WriteLine($"Connecting to {instanceUrl} with API key: {(apiKey != null ? "Yes" : "No")}");
            await ConnectToTimeline(instanceUrl, apiKey);
        }

        private async void ShowAddInstanceDialog(object? sender, RoutedEventArgs e)
        {
            await ShowAddInstanceDialog();
        }
        
        private async Task ShowAddInstanceDialog()
        {
            var urlTextBox = new TextBox { Name = "urlTextBox", Watermark = "mi.ruruke.moe", Margin = new Avalonia.Thickness(0, 0, 0, 10) };
            var apiKeyTextBox = new TextBox { Name = "apiKeyTextBox", Watermark = "APIキー（オプション）", Margin = new Avalonia.Thickness(0, 0, 0, 10) };
            
            var cancelButton = new Button 
            { 
                Content = "キャンセル", 
                Margin = new Avalonia.Thickness(0, 0, 10, 0),
                Background = Avalonia.Media.Brushes.White,
                BorderBrush = Avalonia.Media.Brush.Parse("#8C8C8C"),
                BorderThickness = new Avalonia.Thickness(1),
                Foreground = Avalonia.Media.Brushes.Black
            };
            var addButton = new Button 
            { 
                Content = "追加",
                Background = Avalonia.Media.Brushes.White,
                BorderBrush = Avalonia.Media.Brush.Parse("#8C8C8C"),
                BorderThickness = new Avalonia.Thickness(1),
                Foreground = Avalonia.Media.Brushes.Black
            };
            
            var dialog = new Avalonia.Controls.Window
            {
                Title = "インスタンス追加",
                Width = 400,
                Height = 200,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                CanResize = false,
                ShowInTaskbar = false,
                Background = Avalonia.Media.Brushes.White,
                Content = new StackPanel
                {
                    Margin = new Avalonia.Thickness(20),
                    Children =
                    {
                        new TextBlock { Text = "インスタンスURL:", Margin = new Avalonia.Thickness(0, 0, 0, 5), Foreground = Avalonia.Media.Brushes.Black },
                        urlTextBox,
                        new TextBlock { Text = "APIキー（オプション）:", Margin = new Avalonia.Thickness(0, 0, 0, 5), Foreground = Avalonia.Media.Brushes.Black },
                        apiKeyTextBox,
                        new StackPanel
                        {
                            Orientation = Avalonia.Layout.Orientation.Horizontal,
                            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Right,
                            Children =
                            {
                                cancelButton,
                                addButton
                            }
                        }
                    }
                }
            };

            cancelButton.Click += (s, e) => dialog.Close();
            
            addButton.Click += async (s, e) =>
            {
                try
                {
                    var url = urlTextBox.Text?.Trim();
                    var apiKey = apiKeyTextBox.Text?.Trim();
                    
                    if (string.IsNullOrEmpty(url))
                    {
                        tsLabelMain.Text = "インスタンスURLを入力してください";
                        return;
                    }
                    
                    dialog.Close();
                    await AddInstance(url, string.IsNullOrEmpty(apiKey) ? null : apiKey);
                }
                catch (Exception ex)
                {
                    tsLabelMain.Text = $"エラー: {ex.Message}";
                    dialog.Close();
                }
            };

            await dialog.ShowDialog(this);
        }

        private async Task AddInstance(string instanceUrl, string? apiKey = null)
        {
            try
            {
                tsLabelMain.Text = $"インスタンス {instanceUrl} を追加中...";
                
                // 既に存在する場合は追加しない
                if (_instances.Contains(instanceUrl))
                {
                    tsLabelMain.Text = $"インスタンス {instanceUrl} は既に追加されています";
                    cmbInstanceSelect.SelectedItem = instanceUrl;
                    return;
                }
                
                // インスタンスをリストに追加
                _instances.Add(instanceUrl);
                
                // APIキーを保存
                if (!string.IsNullOrEmpty(apiKey))
                {
                    _instanceTokens[instanceUrl] = apiKey;
                }
                
                // サーバー用のタブを作成
                var serverTabs = new List<string> { "統合TL", "ローカルTL", "ソーシャルTL", "グローバルTL" };
                _serverTabs[instanceUrl] = serverTabs;
                
                // 選択状態にする
                cmbInstanceSelect.SelectedItem = instanceUrl;
                
                tsLabelMain.Text = $"インスタンス {instanceUrl} を追加しました";
                
                // タブを更新
                UpdateTabs(instanceUrl);
                
                // 設定を保存
                SaveSettings();
                
                tsLabelMain.Text = $"インスタンス {instanceUrl} の接続を開始しています...";
                
                // WebSocket接続を開始
                await ConnectToTimeline(instanceUrl, apiKey);
            }
            catch (Exception ex)
            {
                tsLabelMain.Text = $"エラー: {ex.Message}";
                System.Diagnostics.Debug.WriteLine($"AddInstance error: {ex}");
            }
        }

        private async Task ConnectToTimeline(string instanceUrl, string? apiKey = null)
        {
            try
            {
                Console.WriteLine($"ConnectToTimeline called for {instanceUrl}");
                tsLabelMain.Text = "接続中...";
                
                // 既存の接続を切断
                await DisconnectWebSocket();
                
                Console.WriteLine($"Selected tab index: {_selectedTabIndex}");
                
                // 統合TLの場合は複数のタイムラインに接続
                if (_selectedTabIndex == 0) // 統合TL
                {
                    Console.WriteLine("Connecting to unified timeline");
                    _ = Task.Run(async () => await ConnectToUnifiedTimeline(instanceUrl, apiKey));
                }
                else
                {
                    Console.WriteLine("Connecting to single timeline");
                    // 通常の単一タイムライン接続をバックグラウンドで実行
                    _ = Task.Run(async () =>
                    {
                        try
                        {
                            var timelineType = _selectedTabIndex switch
                            {
                                1 => WebSocketTimeLineCommon.ConnectTimeLineKind.Local,
                                2 => WebSocketTimeLineCommon.ConnectTimeLineKind.Social,
                                3 => WebSocketTimeLineCommon.ConnectTimeLineKind.Global,
                                _ => WebSocketTimeLineCommon.ConnectTimeLineKind.Home
                            };
                            
                            _webSocketTimeLine = WebSocketTimeLineCommon.CreateInstance(timelineType);
                            
                            if (_webSocketTimeLine != null)
                            {
                                // イベントハンドラーを設定
                                _webSocketTimeLine.TimeLineDataReceived += OnTimeLineDataReceived;
                                
                                // タイムラインに接続（非同期）
                                await Task.Run(() =>
                                {
                                    try
                                    {
                                        System.Diagnostics.Debug.WriteLine($"Single timeline connecting to {instanceUrl}...");
                                        Console.WriteLine($"Single timeline connecting to {instanceUrl}...");
                                        _webSocketTimeLine.OpenTimeLine(instanceUrl, apiKey);
                                        System.Diagnostics.Debug.WriteLine($"Single timeline connected to {instanceUrl}, starting continuous read...");
                                        Console.WriteLine($"Single timeline connected to {instanceUrl}, starting continuous read...");
                                        WebSocketTimeLineCommon.ReadTimeLineContinuous(_webSocketTimeLine);
                                        System.Diagnostics.Debug.WriteLine($"Single timeline continuous read started for {instanceUrl}");
                                        Console.WriteLine($"Single timeline continuous read started for {instanceUrl}");
                                    }
                                    catch (Exception ex)
                                    {
                                        System.Diagnostics.Debug.WriteLine($"Single timeline error connecting to {instanceUrl}: {ex.Message}");
                                        Console.WriteLine($"Single timeline error connecting to {instanceUrl}: {ex.Message}");
                                        throw;
                                    }
                                });
                                
                                await Dispatcher.UIThread.InvokeAsync(() =>
                                {
                                    tsLabelMain.Text = $"接続成功: {instanceUrl}";
                                });
                            }
                            else
                            {
                                await Dispatcher.UIThread.InvokeAsync(() =>
                                {
                                    tsLabelMain.Text = $"接続失敗: {instanceUrl}";
                                });
                            }
                        }
                        catch (Exception ex)
                        {
                            await Dispatcher.UIThread.InvokeAsync(() =>
                            {
                                tsLabelMain.Text = $"接続エラー: {ex.Message}";
                            });
                        }
                    });
                }
            }
            catch (Exception ex)
            {
                tsLabelMain.Text = $"接続エラー: {ex.Message}";
            }
        }
        
        private async Task ConnectToUnifiedTimeline(string instanceUrl, string? apiKey = null)
        {
            try
            {
                Console.WriteLine("ConnectToUnifiedTimeline started");
                await Dispatcher.UIThread.InvokeAsync(() =>
                {
                    tsLabelMain.Text = "統合TL接続中...";
                });
                
                // 統合TLでは全てのインスタンスのローカルTLに接続
                var connectedInstances = new List<WebSocketTimeLineCommon>();
                
                Console.WriteLine($"Found {_instances.Count} instances to connect to");
                
                foreach (var instance in _instances)
                {
                    try
                    {
                        Console.WriteLine($"Creating timeline for {instance}");
                        var localTimeline = WebSocketTimeLineCommon.CreateInstance(WebSocketTimeLineCommon.ConnectTimeLineKind.Local);
                        if (localTimeline != null)
                        {
                            Console.WriteLine($"Timeline created for {instance}, adding event handler");
                            localTimeline.TimeLineDataReceived += OnTimeLineDataReceived;
                            
                            var instanceApiKey = _instanceTokens.ContainsKey(instance) ? _instanceTokens[instance] : null;
                            Console.WriteLine($"API key for {instance}: {(instanceApiKey != null ? "Yes" : "No")}");
                            
                            // 接続処理を非同期で実行（タイムアウト付き）
                            try
                            {
                                System.Diagnostics.Debug.WriteLine($"Connecting to {instance}...");
                                Console.WriteLine($"Connecting to {instance}...");
                                
                                // タイムアウト付きで接続を試行
                                await Task.Run(() =>
                                {
                                    using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
                                    try
                                    {
                                        localTimeline.OpenTimeLine(instance, instanceApiKey);
                                        Console.WriteLine($"Connected to {instance}, starting continuous read...");
                                        WebSocketTimeLineCommon.ReadTimeLineContinuous(localTimeline);
                                        Console.WriteLine($"Continuous read started for {instance}");
                                    }
                                    catch (Exception ex)
                                    {
                                        Console.WriteLine($"Error in OpenTimeLine for {instance}: {ex.Message}");
                                        throw;
                                    }
                                });
                            }
                            catch (Exception ex)
                            {
                                System.Diagnostics.Debug.WriteLine($"Error connecting to {instance}: {ex.Message}");
                                Console.WriteLine($"Error connecting to {instance}: {ex.Message}");
                                continue; // 他のインスタンスの接続を続行
                            }
                            
                            connectedInstances.Add(localTimeline);
                            
                            // 進捗を更新
                            await Dispatcher.UIThread.InvokeAsync(() =>
                            {
                                tsLabelMain.Text = $"統合TL接続中... ({connectedInstances.Count}/{_instances.Count})";
                            });
                        }
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"Failed to connect to {instance}: {ex.Message}");
                    }
                }
                
                // 統合TL用の接続リストを更新
                _unifiedTimelineConnections.Clear();
                _unifiedTimelineConnections.AddRange(connectedInstances);
                
                await Dispatcher.UIThread.InvokeAsync(() =>
                {
                    if (connectedInstances.Count > 0)
                    {
                        tsLabelMain.Text = $"統合TL接続成功: {connectedInstances.Count}個のインスタンス";
                    }
                    else
                    {
                        tsLabelMain.Text = "統合TL接続失敗: 接続できるインスタンスがありません";
                    }
                });
            }
            catch (Exception ex)
            {
                await Dispatcher.UIThread.InvokeAsync(() =>
                {
                    tsLabelMain.Text = $"統合TL接続エラー: {ex.Message}";
                });
                System.Diagnostics.Debug.WriteLine($"ConnectToUnifiedTimeline error: {ex}");
            }
        }
        
        private async void OnTimeLineDataReceived(object? sender, TimeLineContainer container)
        {
            System.Diagnostics.Debug.WriteLine($"Timeline data received from {container.SOURCE}: {container.DETAIL}");
            Console.WriteLine($"Timeline data received from {container.SOURCE}: {container.DETAIL}");
            
            // UIスレッドで投稿を追加
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                // キャッシュキーを生成（インスタンス名_タブ名）
                var cacheKey = GetCacheKey(container.SOURCE, _selectedTabIndex);
                
                // キャッシュに追加
                if (!_timelineCache.ContainsKey(cacheKey))
                {
                    _timelineCache[cacheKey] = new List<TimeLineContainer>();
                }
                
                _timelineCache[cacheKey].Insert(0, container);
                
                // キャッシュサイズ制限
                if (_timelineCache[cacheKey].Count > MAX_CACHED_ITEMS)
                {
                    _timelineCache[cacheKey].RemoveAt(_timelineCache[cacheKey].Count - 1);
                }
                
                // 現在表示中のタブと一致する場合のみUI更新
                var currentCacheKey = GetCacheKey(GetCurrentInstanceUrl(), _selectedTabIndex);
                if (cacheKey == currentCacheKey)
                {
                    AddTimelineItem(container, container.SOURCE);
                    
                    // 詳細パネルに表示
                    var note = new Note { Node = container.ORIGINAL };
                    SetTimelineDetails(container, note);
                    
                    System.Diagnostics.Debug.WriteLine($"UI updated with data from {container.SOURCE}");
                    Console.WriteLine($"UI updated with data from {container.SOURCE}");
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine($"Data cached but not displayed (current: {currentCacheKey}, received: {cacheKey})");
                }
            });
        }

        private async Task SubscribeToTimeline(string channel)
        {
            if (_webSocket?.State == WebSocketState.Open)
            {
                var subscribeMessage = JsonSerializer.Serialize(new
                {
                    type = "connect",
                    body = new
                    {
                        channel = channel,
                        id = "timeline"
                    }
                });
                
                var buffer = Encoding.UTF8.GetBytes(subscribeMessage);
                await _webSocket.SendAsync(new ArraySegment<byte>(buffer), WebSocketMessageType.Text, true, CancellationToken.None);
            }
        }

        private async Task ReceiveMessages()
        {
            var buffer = new byte[4096];
            
            while (_webSocket?.State == WebSocketState.Open && !_cancellationTokenSource?.Token.IsCancellationRequested == true)
            {
                try
                {
                    var result = await _webSocket.ReceiveAsync(new ArraySegment<byte>(buffer), _cancellationTokenSource!.Token);
                    
                    if (result.MessageType == WebSocketMessageType.Text)
                    {
                        var message = Encoding.UTF8.GetString(buffer, 0, result.Count);
                        await ProcessTimelineMessage(message);
                    }
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    await Dispatcher.UIThread.InvokeAsync(() =>
                    {
                        tsLabelMain.Text = $"受信エラー: {ex.Message}";
                    });
                    break;
                }
            }
        }

        private async Task ProcessTimelineMessage(string message)
        {
            try
            {
                var json = JsonNode.Parse(message);
                
                if (json?["type"]?.ToString() == "channel" && json?["body"]?["type"]?.ToString() == "note")
                {
                    var noteNode = json["body"]["body"];
                    var note = new Note { Node = noteNode };
                    var user = note.User;
                    
                    var username = user.UserName?.ToString() ?? "unknown";
                    var channel = json["body"]?["id"]?.ToString() ?? "homeTimeline";
                    var selectedInstance = cmbInstanceSelect.SelectedItem?.ToString() ?? "misskey.io";
                    
                    // TimeLineContainerを作成
                    var timelineItem = new TimeLineContainer
                    {
                        USERID = user.Id?.ToString() ?? "",
                        USERNAME = username,
                        TLFROM = channel,
                        RENOTED = noteNode?["renote"] != null,
                        REPLAYED = noteNode?["reply"] != null,
                        PROTECTED = TimeLineContainer.PROTECTED_STATUS.Public,
                        ORIGINAL = noteNode ?? JsonNode.Parse("{}")!,
                        DETAIL = note.Text?.ToString() ?? "（内容なし）",
                        UPDATEDAT = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff"),
                        SOURCE = selectedInstance
                    };
                    
                    // UIスレッドで投稿を追加
                    await Dispatcher.UIThread.InvokeAsync(() =>
                    {
                        AddTimelineItem(timelineItem, selectedInstance);
                        SetTimelineDetails(timelineItem, note);
                    });
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"メッセージ処理エラー: {ex.Message}");
            }
        }

        private void SetTimelineDetails(TimeLineContainer timelineItem, Note note)
        {
            lblUser.Text = $"@{timelineItem.USERNAME}";
            lblTLFrom.Text = $"source: {timelineItem.TLFROM}";
            lblSoftware.Text = timelineItem.SOFTWARE != "" ? timelineItem.SOFTWARE : "Misskey";
            
            if (DateTime.TryParse(timelineItem.UPDATEDAT, out var timestamp))
            {
                lblUpdatedAt.Text = timestamp.ToString("yyyy/MM/dd HH:mm:ss");
            }
            else
            {
                lblUpdatedAt.Text = DateTime.Now.ToString("yyyy/MM/dd HH:mm:ss");
            }
            
            txtDetail.Text = timelineItem.DETAIL;
        }

        private async Task DisconnectWebSocket()
        {
            // 統合TLの接続を切断
            foreach (var connection in _unifiedTimelineConnections)
            {
                try
                {
                    connection.TimeLineDataReceived -= OnTimeLineDataReceived;
                    var socket = connection.GetSocketClient();
                    if (socket != null && socket.State == WebSocketState.Open)
                    {
                        await socket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Disconnect", CancellationToken.None);
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Error disconnecting unified timeline: {ex.Message}");
                }
            }
            _unifiedTimelineConnections.Clear();
            
            if (_webSocketTimeLine != null)
            {
                _webSocketTimeLine.TimeLineDataReceived -= OnTimeLineDataReceived;
                // ConnectionAbortはprotectedなので直接呼び出せない
                // 代わりに、WebSocketの状態を確認してクローズする
                var socket = _webSocketTimeLine.GetSocketClient();
                if (socket != null && socket.State == WebSocketState.Open)
                {
                    await socket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Disconnect", CancellationToken.None);
                }
                _webSocketTimeLine = null;
            }
            
            if (_webSocket != null)
            {
                _cancellationTokenSource?.Cancel();
                
                if (_webSocket.State == WebSocketState.Open)
                {
                    await _webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Disconnect", CancellationToken.None);
                }
                
                _webSocket.Dispose();
                _webSocket = null;
            }
            
            _cancellationTokenSource?.Dispose();
            _cancellationTokenSource = null;
        }

        private void OnInstanceSelectionChanged(object? sender, SelectionChangedEventArgs e)
        {
            var selectedInstance = cmbInstanceSelect.SelectedItem?.ToString();
            if (!string.IsNullOrEmpty(selectedInstance))
            {
                tsLabelMain.Text = $"インスタンス {selectedInstance} を選択しました。「接続」ボタンで接続できます。";
                
                // タブを更新
                UpdateTabs(selectedInstance);
            }
        }

        private void UpdateTabs(string instanceUrl)
        {
            if (_serverTabs.ContainsKey(instanceUrl))
            {
                // タブコンテナを取得
                var tabContainer = this.FindControl<StackPanel>("tabContainer");
                if (tabContainer == null)
                {
                    // タブコンテナが見つからない場合は新規作成
                    var tabBorder = this.FindControl<Border>("tabBorder");
                    if (tabBorder != null)
                    {
                        tabContainer = new StackPanel
                        {
                            Name = "tabContainer",
                            Orientation = Avalonia.Layout.Orientation.Horizontal,
                            Height = 22
                        };
                        tabBorder.Child = tabContainer;
                    }
                }
                
                if (tabContainer != null)
                {
                    tabContainer.Children.Clear();
                    
                    var tabs = _serverTabs[instanceUrl];
                    for (int i = 0; i < tabs.Count; i++)
                    {
                        var tabName = tabs[i];
                        var isSelected = i == 0; // 最初のタブを選択状態に
                        
                        var tabBorder = new Border
                        {
                            Background = isSelected ? Avalonia.Media.Brushes.White : Avalonia.Media.Brush.Parse("#F0F0F0"),
                            BorderBrush = Avalonia.Media.Brush.Parse("#8C8C8C"),
                            BorderThickness = new Avalonia.Thickness(1, 0, 1, 1),
                            Padding = new Avalonia.Thickness(8, 2),
                            Cursor = new Avalonia.Input.Cursor(Avalonia.Input.StandardCursorType.Hand)
                        };
                        
                        var tabText = new TextBlock
                        {
                            Text = tabName,
                            FontSize = 11,
                            Foreground = Avalonia.Media.Brushes.Black,
                            VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center
                        };
                        
                        tabBorder.Child = tabText;
                        
                        // クリックイベントを追加
                        var tabIndex = i;
                        tabBorder.PointerPressed += async (sender, e) =>
                        {
                            // 同じタブがクリックされた場合は何もしない
                            if (tabIndex == _selectedTabIndex)
                                return;
                                
                            _selectedTabIndex = tabIndex;
                            await SwitchTab(instanceUrl, tabIndex);
                        };
                        
                        tabContainer.Children.Add(tabBorder);
                        
                        // タブ間のスペース
                        if (i < tabs.Count - 1)
                        {
                            tabContainer.Children.Add(new Border { Background = Avalonia.Media.Brush.Parse("#F0F0F0"), Width = 2 });
                        }
                    }
                }
            }
        }
        
        private async Task SwitchTab(string instanceUrl, int tabIndex)
        {
            try
            {
                // タブの選択状態を即座に更新（UI応答性向上）
                UpdateTabSelection(tabIndex);
                
                var tabName = _serverTabs[instanceUrl][tabIndex];
                tsLabelMain.Text = $"切り替え中: {instanceUrl} - {tabName}";
                
                // 新しいタイムラインタイプに接続
                var timelineType = tabIndex switch
                {
                    0 => WebSocketTimeLineCommon.ConnectTimeLineKind.Home,
                    1 => WebSocketTimeLineCommon.ConnectTimeLineKind.Local,
                    2 => WebSocketTimeLineCommon.ConnectTimeLineKind.Social,
                    3 => WebSocketTimeLineCommon.ConnectTimeLineKind.Global,
                    _ => WebSocketTimeLineCommon.ConnectTimeLineKind.Home
                };
                
                // バックグラウンドで接続処理を実行
                _ = Task.Run(async () =>
                {
                    try
                    {
                        // 既存の接続を切断
                        await DisconnectWebSocket();
                        
                        // タイムラインをクリア（UIスレッドで実行）
                        await Dispatcher.UIThread.InvokeAsync(() =>
                        {
                            timelineContainer.Children.Clear();
                            _timelineItems.Clear();
                            _noteCount = 0;
                            
                            // キャッシュからデータを復元
                            var cacheKey = GetCacheKey(instanceUrl, tabIndex);
                            if (_timelineCache.ContainsKey(cacheKey))
                            {
                                var cachedItems = _timelineCache[cacheKey];
                                foreach (var item in cachedItems.AsEnumerable().Reverse())
                                {
                                    AddTimelineItem(item, item.SOURCE);
                                }
                            }
                            
                            tsLabelNoteCount.Text = $"{_noteCount}/{MAX_CACHED_ITEMS}";
                        });
                        
                        // 新しい接続を開始
                        _webSocketTimeLine = WebSocketTimeLineCommon.CreateInstance(timelineType);
                        
                        if (_webSocketTimeLine != null)
                        {
                            _webSocketTimeLine.TimeLineDataReceived += OnTimeLineDataReceived;
                            
                            // APIキーを取得
                            var apiKey = _instanceTokens.ContainsKey(instanceUrl) ? _instanceTokens[instanceUrl] : null;
                            
                            _webSocketTimeLine.OpenTimeLine(instanceUrl, apiKey);
                            WebSocketTimeLineCommon.ReadTimeLineContinuous(_webSocketTimeLine);
                            
                            await Dispatcher.UIThread.InvokeAsync(() =>
                            {
                                tsLabelMain.Text = $"接続成功: {instanceUrl} - {tabName}";
                            });
                        }
                    }
                    catch (Exception ex)
                    {
                        await Dispatcher.UIThread.InvokeAsync(() =>
                        {
                            tsLabelMain.Text = $"タブ切り替えエラー: {ex.Message}";
                        });
                    }
                });
            }
            catch (Exception ex)
            {
                tsLabelMain.Text = $"タブ切り替えエラー: {ex.Message}";
            }
        }
        
        private void UpdateTabSelection(int selectedIndex)
        {
            var tabContainer = this.FindControl<StackPanel>("tabContainer");
            if (tabContainer != null)
            {
                int tabCount = 0;
                foreach (var child in tabContainer.Children)
                {
                    if (child is Border border && border.Child is TextBlock)
                    {
                        border.Background = tabCount == selectedIndex ? Avalonia.Media.Brushes.White : Avalonia.Media.Brush.Parse("#F0F0F0");
                        tabCount++;
                    }
                }
            }
        }

        private void LoadSettings()
        {
            try
            {
                if (File.Exists(SETTINGS_FILE))
                {
                    var json = File.ReadAllText(SETTINGS_FILE);
                    var settings = JsonSerializer.Deserialize<AppSettings>(json);
                    
                    if (settings != null)
                    {
                        // インスタンスを復元
                        foreach (var instance in settings.Instances)
                        {
                            _instances.Add(instance);
                        }
                        
                        // APIキーを復元
                        foreach (var token in settings.InstanceTokens)
                        {
                            _instanceTokens[token.Key] = token.Value;
                        }
                        
                        // サーバータブを復元
                        foreach (var instance in settings.Instances)
                        {
                            var serverTabs = new List<string> { "統合TL", "ローカルTL", "ソーシャルTL", "グローバルTL" };
                            _serverTabs[instance] = serverTabs;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                tsLabelMain.Text = $"設定読み込みエラー: {ex.Message}";
            }
        }
        
        private void SaveSettings()
        {
            try
            {
                var settings = new AppSettings
                {
                    Instances = _instances.ToList(),
                    InstanceTokens = _instanceTokens
                };
                
                var json = JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(SETTINGS_FILE, json);
            }
            catch (Exception ex)
            {
                tsLabelMain.Text = $"設定保存エラー: {ex.Message}";
            }
        }

        private string GetCacheKey(string instanceUrl, int tabIndex)
        {
            return $"{instanceUrl}_{tabIndex}";
        }
        
        private string GetCurrentInstanceUrl()
        {
            return cmbInstanceSelect.SelectedItem?.ToString() ?? "";
        }

        protected override void OnClosed(EventArgs e)
        {
            SaveSettings();
            DisconnectWebSocket().Wait();
            base.OnClosed(e);
        }
    }
    
    public class AppSettings
    {
        public List<string> Instances { get; set; } = new List<string>();
        public Dictionary<string, string> InstanceTokens { get; set; } = new Dictionary<string, string>();
    }
}