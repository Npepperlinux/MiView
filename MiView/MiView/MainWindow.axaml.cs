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
using MiView.Common.Fonts;
using MiView.Common.Fonts.Material;

namespace MiView
{
    public partial class MainWindow : Avalonia.Controls.Window
    {
        private ClientWebSocket? _webSocket;
        private CancellationTokenSource? _cancellationTokenSource;
        private ObservableCollection<string> _instances = new();
        private ObservableCollection<TimeLineContainer> _timelineData = new();
        private Dictionary<string, List<string>> _serverTabs = new();
        private Dictionary<string, string> _instanceTokens = new();
        private const string SETTINGS_FILE = "settings.json";
        private int _selectedTabIndex = 0;
        private int _noteCount = 0;
        private List<TimeLineContainer> _timelineItems = new();
        private WebSocketTimeLineCommon? _webSocketTimeLine;
        private Dictionary<string, List<TimeLineContainer>> _timelineCache = new();
        private Dictionary<string, Dictionary<string, List<TimeLineContainer>>> _timelineCacheByType = new(); // インスタンス別 → タイムライン種別 → データ
        private const int MAX_CACHED_ITEMS = 1000;
        private List<WebSocketTimeLineCommon> _unifiedTimelineConnections = new();
        private FontLoader _fontLoader = new();
        
        // 常時接続用WebSocket管理
        private Dictionary<string, Dictionary<string, WebSocketTimeLineCommon>> _persistentConnections = new(); // インスタンス → タイムライン種別 → WebSocket
        private Timer? _reconnectTimer;
        private bool _isConnected = false;
        private CancellationTokenSource? _tabSwitchCancellation;

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
            tsLabelMain.Text = "MiView - 起動中...";
            tsLabelNoteCount.Text = "0/0";
            
            // 設定を読み込み
            LoadSettings();
            
            // テスト用の投稿を追加
            AddTestTimelineItems();
            
            // 常時接続を開始
            StartPersistentConnections();
            
            // 再接続タイマーを開始
            StartReconnectTimer();
            
            // 起動時に接続状態を設定
            Task.Delay(3000).ContinueWith(_ => 
            {
                Dispatcher.UIThread.InvokeAsync(() =>
                {
                    if (_instances.Count > 0)
                    {
                        _isConnected = true;
                        cmdConnect.Content = "切断";
                        tsLabelMain.Text = $"準備完了";
                    }
                });
            });
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
                
                // サンプルデータもキャッシュに追加
                var cacheKey = GetCacheKey(instance, _selectedTabIndex);
                if (!_timelineCache.ContainsKey(cacheKey))
                {
                    _timelineCache[cacheKey] = new List<TimeLineContainer>();
                }
                _timelineCache[cacheKey].Insert(0, timelineItem);
                
                // ObservableCollectionにも追加
                _timelineData.Insert(0, timelineItem);
            }
            
        }

        private void AddTimelineItem(TimeLineContainer timelineItem, string instance = "misskey.io")
        {
            // TimeLineContainerからNoteオブジェクトを作成
            var note = new Note { Node = timelineItem.ORIGINAL };
            
            // 背景色を決定（Renote、交互の行色）
            Avalonia.Media.IBrush backgroundColor;
            if (timelineItem.RENOTED)
            {
                backgroundColor = Avalonia.Media.Brush.Parse("#E8F5E8"); // 薄緑色（Renote）
            }
            else
            {
                var isEvenRow = (_noteCount % 2 == 0);
                backgroundColor = isEvenRow ? Avalonia.Media.Brushes.White : Avalonia.Media.Brush.Parse("#F5F5F5");
            }
            
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
                if (timelineItem.RENOTED)
                {
                    timelineGrid.Background = Avalonia.Media.Brush.Parse("#D4F4D4"); // 濃い緑色（Renoteホバー）
                }
                else
                {
                    timelineGrid.Background = Avalonia.Media.Brush.Parse("#E8F4FD"); // 青色（通常ホバー）
                }
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
                    Background = Avalonia.Media.Brushes.Transparent,
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
                
                // MaterialIconsフォントを取得
                var materialIconFont = _fontLoader.LoadFontFamilyFromFile(FontLoader.FONT_SELECTOR.MATERIALICONS);

                switch (i)
                {
                    case 0:
                        // Status icon based on timeline item properties
                        if (timelineItem.RENOTED)
                        {
                            textBlock.Text = MaterialIcons.Repeat;
                            textBlock.FontFamily = materialIconFont;
                            textBlock.Foreground = Avalonia.Media.Brush.Parse("#4CAF50");
                        }
                        else if (timelineItem.REPLAYED)
                        {
                            textBlock.Text = MaterialIcons.Reply;
                            textBlock.FontFamily = materialIconFont;
                            textBlock.Foreground = Avalonia.Media.Brush.Parse("#2196F3");
                        }
                        else
                        {
                            textBlock.Text = MaterialIcons.Circle;
                            textBlock.FontFamily = materialIconFont;
                            textBlock.Foreground = Avalonia.Media.Brush.Parse("#9E9E9E");
                        }
                        textBlock.FontSize = 14;
                        textBlock.HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center;
                        break;
                    case 1:
                        // Federation status icon (宇宙船マーク)
                        textBlock.Text = MaterialIcons.Rocket;
                        textBlock.FontFamily = materialIconFont;
                        textBlock.FontSize = 14;
                        textBlock.HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center;
                        
                        // 連合しているかどうかで色を変更（SOURCEが現在のインスタンスと異なれば連合）
                        var currentInstance = GetCurrentInstanceUrl();
                        if (!string.IsNullOrEmpty(timelineItem.SOURCE) && timelineItem.SOURCE != currentInstance)
                        {
                            textBlock.Foreground = Avalonia.Media.Brush.Parse("#4CAF50"); // 緑（連合）
                        }
                        else
                        {
                            textBlock.Foreground = Avalonia.Media.Brush.Parse("#F44336"); // 赤（非連合）
                        }
                        break;
                    case 2:
                        textBlock.Text = timelineItem.USERNAME;
                        textBlock.FontWeight = Avalonia.Media.FontWeight.Bold;
                        textBlock.Foreground = Avalonia.Media.Brush.Parse("#000000");
                        break;
                    case 3:
                        textBlock.Text = timelineItem.DETAIL;
                        textBlock.TextWrapping = Avalonia.Media.TextWrapping.Wrap;
                        textBlock.Foreground = Avalonia.Media.Brush.Parse("#000000");
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
#if DEBUG
            Console.WriteLine("Connect button clicked!");
#endif
            var instanceUrl = cmbInstanceSelect.SelectedItem?.ToString()?.Trim();
#if DEBUG
            Console.WriteLine($"Selected instance: {instanceUrl}");
#endif
            
            if (_isConnected)
            {
                // 切断処理
                await DisconnectWebSocket();
                cmdConnect.Content = "接続";
                _isConnected = false;
                tsLabelMain.Text = "切断しました";
                return;
            }
            
            if (string.IsNullOrEmpty(instanceUrl))
            {
#if DEBUG
                Console.WriteLine("No instance selected, showing add dialog");
#endif
                // 新しいインスタンスを追加するためのダイアログを表示
                await ShowAddInstanceDialog();
                return;
            }
            
            // 既存のインスタンスの場合は接続
            var apiKey = _instanceTokens.ContainsKey(instanceUrl) ? _instanceTokens[instanceUrl] : null;
#if DEBUG
            Console.WriteLine($"Connecting to {instanceUrl} with API key: {(apiKey != null ? "Yes" : "No")}");
#endif
            await ConnectToTimeline(instanceUrl, apiKey);
        }

        private async void ShowAddInstanceDialog(object? sender, RoutedEventArgs e)
        {
            await ShowAddInstanceDialog();
        }
        
        private async Task ShowAddInstanceDialog()
        {
            var urlTextBox = new TextBox { 
                Name = "urlTextBox", 
                Watermark = "mi.ruruke.moe", 
                Margin = new Avalonia.Thickness(0, 0, 0, 10),
                Background = Avalonia.Media.Brush.Parse("#FFFFFF"),
                Foreground = Avalonia.Media.Brush.Parse("#000000"),
                BorderBrush = Avalonia.Media.Brush.Parse("#8C8C8C"),
                BorderThickness = new Avalonia.Thickness(1)
            };
            var apiKeyTextBox = new TextBox { 
                Name = "apiKeyTextBox", 
                Watermark = "APIキー（オプション）", 
                Margin = new Avalonia.Thickness(0, 0, 0, 10),
                Background = Avalonia.Media.Brush.Parse("#FFFFFF"),
                Foreground = Avalonia.Media.Brush.Parse("#000000"),
                BorderBrush = Avalonia.Media.Brush.Parse("#8C8C8C"),
                BorderThickness = new Avalonia.Thickness(1)
            };
            
            var cancelButton = new Button 
            { 
                Content = "キャンセル", 
                Margin = new Avalonia.Thickness(0, 0, 10, 0),
                Background = Avalonia.Media.Brush.Parse("#FFFFFF"),
                BorderBrush = Avalonia.Media.Brush.Parse("#8C8C8C"),
                BorderThickness = new Avalonia.Thickness(1),
                Foreground = Avalonia.Media.Brush.Parse("#000000"),
                Padding = new Avalonia.Thickness(15, 5)
            };
            var addButton = new Button 
            { 
                Content = "追加",
                Background = Avalonia.Media.Brush.Parse("#FFFFFF"),
                BorderBrush = Avalonia.Media.Brush.Parse("#8C8C8C"),
                BorderThickness = new Avalonia.Thickness(1),
                Foreground = Avalonia.Media.Brush.Parse("#000000"),
                Padding = new Avalonia.Thickness(15, 5)
            };
            
            var dialog = new Avalonia.Controls.Window
            {
                Title = "インスタンス追加",
                Width = 400,
                Height = 200,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                CanResize = false,
                ShowInTaskbar = false,
                Background = Avalonia.Media.Brush.Parse("#F0F0F0"),
                Content = new StackPanel
                {
                    Margin = new Avalonia.Thickness(20),
                    Background = Avalonia.Media.Brushes.Transparent,
                    Children =
                    {
                        new TextBlock { 
                            Text = "インスタンスURL:", 
                            Margin = new Avalonia.Thickness(0, 0, 0, 5), 
                            Foreground = Avalonia.Media.Brush.Parse("#000000"),
                            Background = Avalonia.Media.Brushes.Transparent
                        },
                        urlTextBox,
                        new TextBlock { 
                            Text = "APIキー（オプション）:", 
                            Margin = new Avalonia.Thickness(0, 0, 0, 5), 
                            Foreground = Avalonia.Media.Brush.Parse("#000000"),
                            Background = Avalonia.Media.Brushes.Transparent
                        },
                        apiKeyTextBox,
                        new StackPanel
                        {
                            Orientation = Avalonia.Layout.Orientation.Horizontal,
                            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Right,
                            Background = Avalonia.Media.Brushes.Transparent,
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
#if DEBUG
                Console.WriteLine($"ConnectToTimeline called for {instanceUrl}");
#endif
                tsLabelMain.Text = "接続中...";
                
                // 既存の接続を切断
                await DisconnectWebSocket();
                
#if DEBUG
                Console.WriteLine($"Selected tab index: {_selectedTabIndex}");
#endif
                
                // 統合TLの場合は複数のタイムラインに接続
                if (_selectedTabIndex == 0) // 統合TL
                {
#if DEBUG
                    Console.WriteLine("Connecting to unified timeline");
#endif
                    _ = Task.Run(async () => await ConnectToUnifiedTimeline(instanceUrl, apiKey));
                }
                else
                {
#if DEBUG
                    Console.WriteLine("Connecting to single timeline");
#endif
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
#if DEBUG
                                        Console.WriteLine($"Single timeline connecting to {instanceUrl}...");
#endif
                                        _webSocketTimeLine.OpenTimeLine(instanceUrl, apiKey);
                                        System.Diagnostics.Debug.WriteLine($"Single timeline connected to {instanceUrl}, starting continuous read...");
#if DEBUG
                                        Console.WriteLine($"Single timeline connected to {instanceUrl}, starting continuous read...");
#endif
                                        WebSocketTimeLineCommon.ReadTimeLineContinuous(_webSocketTimeLine);
                                        System.Diagnostics.Debug.WriteLine($"Single timeline continuous read started for {instanceUrl}");
#if DEBUG
                                        Console.WriteLine($"Single timeline continuous read started for {instanceUrl}");
#endif
                                    }
                                    catch (Exception ex)
                                    {
                                        System.Diagnostics.Debug.WriteLine($"Single timeline error connecting to {instanceUrl}: {ex.Message}");
#if DEBUG
                                        Console.WriteLine($"Single timeline error connecting to {instanceUrl}: {ex.Message}");
#endif
                                        throw;
                                    }
                                });
                                
                                await Dispatcher.UIThread.InvokeAsync(() =>
                                {
                                    tsLabelMain.Text = $"接続成功: {instanceUrl}";
                                    cmdConnect.Content = "切断";
                                    _isConnected = true;
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
#if DEBUG
                Console.WriteLine("ConnectToUnifiedTimeline started");
#endif
                await Dispatcher.UIThread.InvokeAsync(() =>
                {
                    tsLabelMain.Text = "統合TL接続中...（持続接続のソーシャルTLを使用）";
                });
                
                // 統合TLは持続接続で既に確立されたソーシャルTL接続を使用
                var connectedInstances = new List<WebSocketTimeLineCommon>();
                
                foreach (var instance in _instances)
                {
                    if (_persistentConnections.ContainsKey(instance) && 
                        _persistentConnections[instance].ContainsKey("ソーシャルTL"))
                    {
                        var socialConnection = _persistentConnections[instance]["ソーシャルTL"];
                        connectedInstances.Add(socialConnection);
#if DEBUG
                        Console.WriteLine($"Using existing persistent Social TL connection for {instance}");
#endif
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
                        cmdConnect.Content = "切断";
                        _isConnected = true;
                    }
                    else
                    {
                        tsLabelMain.Text = "統合TL接続失敗: 持続接続がまだ確立されていません";
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
#if DEBUG
            Console.WriteLine($"Timeline data received from {container.SOURCE}: {container.DETAIL}");
#endif
            
            // UIスレッドで投稿を追加
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                // キャッシュキーを生成（インスタンス名_タブ名）
                var instanceName = string.IsNullOrEmpty(container.SOURCE) ? GetCurrentInstanceUrl() : container.SOURCE;
                
                // 統合TL接続中の場合は、常にタブインデックス0として処理
                var effectiveTabIndex = _selectedTabIndex;
#if DEBUG
                Console.WriteLine($"Current _selectedTabIndex: {_selectedTabIndex}, unified connections count: {_unifiedTimelineConnections.Count}");
#endif
                if (_unifiedTimelineConnections.Count > 0) // 統合TL接続中
                {
                    effectiveTabIndex = 0;
#if DEBUG
                    Console.WriteLine($"Using effectiveTabIndex=0 for unified timeline");
#endif
                }
                
                var cacheKey = GetCacheKey(instanceName, effectiveTabIndex);
                
                // タイムライン種別ごとの保存を追加（統合TLの場合はソーシャルTLとして保存）
                SaveToTimelineCacheByType(instanceName, container, effectiveTabIndex);
                
                // ソーシャルTLデータの場合は統合TL用にも保存（統合TLで表示するため）
                if (effectiveTabIndex == 0 && _unifiedTimelineConnections.Count > 0) // 統合TL接続中のソーシャルTLデータ
                {
                    // ソーシャルTLタブ（インデックス2）用にも保存（重複チェック付き）
                    SaveToTimelineCacheByTypeIfNotExists(instanceName, container, 2);
                }
                
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
                
                // UI更新条件を修正：統合TL接続中はすべてのソーシャルTLデータを表示
                var shouldUpdateUI = false;
                if (_unifiedTimelineConnections.Count > 0 && _selectedTabIndex == 0) // 統合TL表示中
                {
                    shouldUpdateUI = true;
#if DEBUG
                    Console.WriteLine($"Unified TL: showing data from {instanceName}");
#endif
                }
                else
                {
                    // 通常のタブ表示：現在のタブと一致する場合のみ表示
                    var currentCacheKey = GetCacheKey(GetCurrentInstanceUrl(), _selectedTabIndex);
                    shouldUpdateUI = (cacheKey == currentCacheKey);
#if DEBUG
                    Console.WriteLine($"Normal tab: received='{cacheKey}', current='{currentCacheKey}', match={shouldUpdateUI}");
#endif
                }
                
                if (shouldUpdateUI)
                {
                    // 初回メッセージを削除（WebSocketで初めてデータを受信した場合）
                    var loadingMessage = timelineContainer.Children.OfType<TextBlock>()
                        .FirstOrDefault(tb => tb.Text == "タイムラインを読み込み中...");
                    if (loadingMessage != null)
                    {
                        timelineContainer.Children.Remove(loadingMessage);
                    }
                    
                    // ObservableCollectionに追加
                    _timelineData.Insert(0, container);
                    
                    // UIスレッドでの表示サイズ制限
                    if (_timelineData.Count > MAX_CACHED_ITEMS)
                    {
                        _timelineData.RemoveAt(_timelineData.Count - 1);
                    }
                    
                    // SOURCEが空の場合は現在のインスタンス名を設定
                    if (string.IsNullOrEmpty(container.SOURCE))
                    {
                        container.SOURCE = instanceName;
                    }
                    
                    AddTimelineItem(container, instanceName);
                    
                    // 詳細パネルに表示
                    var note = new Note { Node = container.ORIGINAL };
                    SetTimelineDetails(container, note);
                    
                    System.Diagnostics.Debug.WriteLine($"UI updated with data from {container.SOURCE}. Timeline children count: {timelineContainer.Children.Count}");
#if DEBUG
                    Console.WriteLine($"UI updated with data from {container.SOURCE}. Timeline children count: {timelineContainer.Children.Count}");
#endif
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine($"Data cached but not displayed");
                }
            });
        }
        
        private void SaveToTimelineCacheByType(string instanceName, TimeLineContainer container, int tabIndex)
        {
            // インスタンス別キャッシュを初期化
            if (!_timelineCacheByType.ContainsKey(instanceName))
            {
                _timelineCacheByType[instanceName] = new Dictionary<string, List<TimeLineContainer>>();
            }
            
            // タイムライン種別を現在のタブから判定
            string timelineType = tabIndex switch
            {
                0 => "ソーシャルTL", // 統合TL（ソーシャルTLデータ）
                1 => "ローカルTL",
                2 => "ソーシャルTL",
                3 => "グローバルTL",
                _ => "その他"
            };
#if DEBUG
            Console.WriteLine($"SaveToTimelineCacheByType: tabIndex={tabIndex}, timelineType={timelineType}, instanceName={instanceName}");
#endif
            
            // タイムライン種別ごとのリストを初期化
            if (!_timelineCacheByType[instanceName].ContainsKey(timelineType))
            {
                _timelineCacheByType[instanceName][timelineType] = new List<TimeLineContainer>();
            }
            
            // データを保存
            _timelineCacheByType[instanceName][timelineType].Insert(0, container);
            
            // サイズ制限
            if (_timelineCacheByType[instanceName][timelineType].Count > MAX_CACHED_ITEMS)
            {
                _timelineCacheByType[instanceName][timelineType].RemoveAt(_timelineCacheByType[instanceName][timelineType].Count - 1);
            }
        }
        
        private void SaveToTimelineCacheByTypeIfNotExists(string instanceName, TimeLineContainer container, int tabIndex)
        {
            // インスタンス別キャッシュを初期化
            if (!_timelineCacheByType.ContainsKey(instanceName))
            {
                _timelineCacheByType[instanceName] = new Dictionary<string, List<TimeLineContainer>>();
            }
            
            // タイムライン種別を現在のタブから判定
            string timelineType = tabIndex switch
            {
                0 => "ソーシャルTL", // 統合TL（ソーシャルTLデータ）
                1 => "ローカルTL",
                2 => "ソーシャルTL",
                3 => "グローバルTL",
                _ => "その他"
            };
            
            // タイムライン種別ごとのリストを初期化
            if (!_timelineCacheByType[instanceName].ContainsKey(timelineType))
            {
                _timelineCacheByType[instanceName][timelineType] = new List<TimeLineContainer>();
            }
            
            // 重複チェック：同じDETAILとUPDATEDATの組み合わせがあるかチェック
            var existingItems = _timelineCacheByType[instanceName][timelineType];
            bool isDuplicate = existingItems.Any(item => 
                item.DETAIL == container.DETAIL && 
                item.UPDATEDAT == container.UPDATEDAT &&
                item.USERNAME == container.USERNAME);
            
            if (!isDuplicate)
            {
                // データを保存
                _timelineCacheByType[instanceName][timelineType].Insert(0, container);
                
                // サイズ制限
                if (_timelineCacheByType[instanceName][timelineType].Count > MAX_CACHED_ITEMS)
                {
                    _timelineCacheByType[instanceName][timelineType].RemoveAt(_timelineCacheByType[instanceName][timelineType].Count - 1);
                }
                
#if DEBUG
                Console.WriteLine($"Added to cache (no duplicate): {timelineType} - {instanceName}");
#endif
            }
            else
            {
#if DEBUG
                Console.WriteLine($"Skipped duplicate entry: {timelineType} - {instanceName}");
#endif
            }
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

        private async void OnInstanceSelectionChanged(object? sender, SelectionChangedEventArgs e)
        {
            var selectedInstance = cmbInstanceSelect.SelectedItem?.ToString();
            if (!string.IsNullOrEmpty(selectedInstance))
            {
                tsLabelMain.Text = $"サーバー {selectedInstance} を選択しました。";
                
                // タブを更新
                UpdateTabs(selectedInstance);
                
                // 現在のタブに応じてタイムラインを切り替え（WebSocketは切断しない）
                await SwitchTab(selectedInstance, _selectedTabIndex);
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
                            _timelineData.Clear();
                            _noteCount = 0;
                            
                            // タイムライン種別ごとのキャッシュからデータを復元
                            var tabName = tabIndex switch
                            {
                                0 => "統合TL",
                                1 => "ローカルTL", 
                                2 => "ソーシャルTL",
                                3 => "グローバルTL",
                                _ => "統合TL"
                            };
                            
                            // 統合TLの場合は特別処理
                            if (tabIndex == 0)
                            {
                                // 全インスタンスのソーシャルTLデータを統合表示
                                var allSocialData = new List<TimeLineContainer>();
                                foreach (var instance in _timelineCacheByType.Keys)
                                {
                                    if (_timelineCacheByType[instance].ContainsKey("ソーシャルTL"))
                                    {
                                        allSocialData.AddRange(_timelineCacheByType[instance]["ソーシャルTL"]);
                                    }
                                }
                                
                                // 時系列でソート
                                allSocialData = allSocialData.OrderByDescending(x => x.UPDATEDAT).Take(MAX_CACHED_ITEMS).ToList();
                                
                                foreach (var item in allSocialData)
                                {
                                    _timelineData.Add(item);
                                }
                                foreach (var item in allSocialData.AsEnumerable().Reverse())
                                {
                                    AddTimelineItem(item, item.SOURCE);
                                }
                            }
                            else
                            {
                                // 個別タイムライン表示
                                if (_timelineCacheByType.ContainsKey(instanceUrl) && 
                                    _timelineCacheByType[instanceUrl].ContainsKey(tabName))
                                {
                                    var cachedItems = _timelineCacheByType[instanceUrl][tabName];
                                    foreach (var item in cachedItems)
                                    {
                                        _timelineData.Add(item);
                                    }
                                    foreach (var item in cachedItems.AsEnumerable().Reverse())
                                    {
                                        AddTimelineItem(item, item.SOURCE);
                                    }
                                }
                                else
                                {
                                    // キャッシュがない場合はサンプルデータを表示
                                    AddTestTimelineItems();
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
                                cmdConnect.Content = "切断";
                                _isConnected = true;
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
            _reconnectTimer?.Dispose();
            base.OnClosed(e);
        }

        private async void ShowServerManagement(object? sender, RoutedEventArgs e)
        {
            var serverManagementWindow = new ServerManagementWindow(_instances, _instanceTokens);
            
            var result = await serverManagementWindow.ShowDialog<bool?>(this);
            
            if (result == true)
            {
                // サーバー管理ウィンドウで変更があった場合
                LoadSettings();
                RefreshInstanceList();
            }
        }
        
        private void RefreshInstanceList()
        {
            _instances.Clear();
            if (File.Exists(SETTINGS_FILE))
            {
                try
                {
                    var jsonString = File.ReadAllText(SETTINGS_FILE);
                    var settings = JsonSerializer.Deserialize<Dictionary<string, object>>(jsonString);
                    
                    if (settings != null)
                    {
                        foreach (var kvp in settings)
                        {
                            if (kvp.Key.StartsWith("instance_"))
                            {
                                var instanceName = kvp.Key.Substring("instance_".Length);
                                _instances.Add(instanceName);
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error loading instances: {ex.Message}");
                }
            }
        }
        
        private void StartPersistentConnections()
        {
            Task.Run(async () =>
            {
                foreach (var instanceName in _instances)
                {
                    await ConnectPersistentInstance(instanceName);
                }
            });
        }
        
        private async Task ConnectPersistentInstance(string instanceName)
        {
            if (!_persistentConnections.ContainsKey(instanceName))
            {
                _persistentConnections[instanceName] = new Dictionary<string, WebSocketTimeLineCommon>();
            }
            
            // 統合TLはソーシャルTLを使用するため、持続接続ではソーシャルTLを除外
            var timelineTypes = new[]
            {
                ("ローカルTL", WebSocketTimeLineCommon.ConnectTimeLineKind.Local),
                ("グローバルTL", WebSocketTimeLineCommon.ConnectTimeLineKind.Global),
                ("ホームTL", WebSocketTimeLineCommon.ConnectTimeLineKind.Home)
            };
            
            foreach (var (timelineType, kind) in timelineTypes)
            {
                try
                {
                    var connection = WebSocketTimeLineCommon.CreateInstance(kind);
                    if (connection != null)
                    {
                        connection.TimeLineDataReceived += OnPersistentTimeLineDataReceived;
                        
                        var apiKey = _instanceTokens.ContainsKey(instanceName) ? _instanceTokens[instanceName] : null;
                        
                        await Task.Run(() =>
                        {
                            try
                            {
                                connection.OpenTimeLine(instanceName, apiKey);
                                WebSocketTimeLineCommon.ReadTimeLineContinuous(connection);
                                
                                _persistentConnections[instanceName][timelineType] = connection;
                                Console.WriteLine($"Persistent connection established: {instanceName} - {timelineType}");
                            }
                            catch (Exception ex)
                            {
                                Console.WriteLine($"Failed to connect {instanceName} - {timelineType}: {ex.Message}");
                            }
                        });
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error creating connection for {instanceName} - {timelineType}: {ex.Message}");
                }
                
                // 接続間隔を開ける
                await Task.Delay(1000);
            }
        }
        
        private async void OnPersistentTimeLineDataReceived(object? sender, TimeLineContainer container)
        {
            // 持続接続からのデータはキャッシュのみに保存し、UI更新は行わない
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                var instanceName = string.IsNullOrEmpty(container.SOURCE) ? GetCurrentInstanceUrl() : container.SOURCE;
                
                // データの種別を判定してキャッシュに保存
                var timelineType = DetermineTimelineTypeFromSource(sender, instanceName);
                if (!string.IsNullOrEmpty(timelineType))
                {
                    SaveToTimelineCacheByTypeWithName(instanceName, container, timelineType);
                    Console.WriteLine($"Persistent cached (no UI): {instanceName} - {timelineType} - {container.DETAIL?.Substring(0, Math.Min(50, container.DETAIL?.Length ?? 0))}");
                }
            });
        }
        
        private string DetermineTimelineTypeFromSource(object? sender, string instanceName)
        {
            // sender（WebSocketTimeLineCommon）から接続種別を特定
            if (_persistentConnections.ContainsKey(instanceName))
            {
                foreach (var kvp in _persistentConnections[instanceName])
                {
                    if (kvp.Value == sender)
                    {
                        return kvp.Key;
                    }
                }
            }
            
            // 統合TLの接続もチェック
            if (_unifiedTimelineConnections.Contains(sender as WebSocketTimeLineCommon))
            {
                return "ソーシャルTL";
            }
            
            return "ソーシャルTL"; // デフォルト
        }
        
        private void SaveToTimelineCacheByTypeWithName(string instanceName, TimeLineContainer container, string timelineType)
        {
            // インスタンス別キャッシュを初期化
            if (!_timelineCacheByType.ContainsKey(instanceName))
            {
                _timelineCacheByType[instanceName] = new Dictionary<string, List<TimeLineContainer>>();
            }
            
            // タイムライン種別ごとのリストを初期化
            if (!_timelineCacheByType[instanceName].ContainsKey(timelineType))
            {
                _timelineCacheByType[instanceName][timelineType] = new List<TimeLineContainer>();
            }
            
            // 重複チェック
            var existingItems = _timelineCacheByType[instanceName][timelineType];
            bool isDuplicate = existingItems.Any(item => 
                item.DETAIL == container.DETAIL && 
                item.UPDATEDAT == container.UPDATEDAT &&
                item.USERNAME == container.USERNAME);
            
            if (!isDuplicate)
            {
                // データを保存
                _timelineCacheByType[instanceName][timelineType].Insert(0, container);
                
                // サイズ制限
                if (_timelineCacheByType[instanceName][timelineType].Count > MAX_CACHED_ITEMS)
                {
                    _timelineCacheByType[instanceName][timelineType].RemoveAt(_timelineCacheByType[instanceName][timelineType].Count - 1);
                }
            }
        }
        
        private void StartReconnectTimer()
        {
            _reconnectTimer = new Timer(CheckAndReconnect, null, TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(1));
        }
        
        private void CheckAndReconnect(object? state)
        {
            Task.Run(async () =>
            {
                foreach (var instanceName in _instances.ToList())
                {
                    if (_persistentConnections.ContainsKey(instanceName))
                    {
                        var connectionsToReconnect = new List<string>();
                        
                        foreach (var kvp in _persistentConnections[instanceName].ToList())
                        {
                            var timelineType = kvp.Key;
                            var connection = kvp.Value;
                            
                            // 接続状態をチェック（簡易的な実装）
                            if (connection == null || !IsConnectionAlive(connection))
                            {
                                connectionsToReconnect.Add(timelineType);
                                Console.WriteLine($"Connection lost, will reconnect: {instanceName} - {timelineType}");
                            }
                        }
                        
                        // 切断された接続を再接続
                        foreach (var timelineType in connectionsToReconnect)
                        {
                            try
                            {
                                await ReconnectTimeline(instanceName, timelineType);
                            }
                            catch (Exception ex)
                            {
                                Console.WriteLine($"Reconnection failed: {instanceName} - {timelineType}: {ex.Message}");
                            }
                        }
                    }
                }
            });
        }
        
        private bool IsConnectionAlive(WebSocketTimeLineCommon connection)
        {
            // 接続状態の簡易チェック（実際の実装では適切なチェック方法を使用）
            try
            {
                return connection != null;
            }
            catch
            {
                return false;
            }
        }
        
        private async Task ReconnectTimeline(string instanceName, string timelineType)
        {
            // ソーシャルTLは統合TLが担当するため、持続接続では再接続しない
            if (timelineType == "ソーシャルTL")
            {
                Console.WriteLine($"Skipping reconnection for Social TL (handled by unified TL): {instanceName}");
                return;
            }
            
            var kind = timelineType switch
            {
                "ローカルTL" => WebSocketTimeLineCommon.ConnectTimeLineKind.Local,
                "グローバルTL" => WebSocketTimeLineCommon.ConnectTimeLineKind.Global,
                "ホームTL" => WebSocketTimeLineCommon.ConnectTimeLineKind.Home,
                _ => WebSocketTimeLineCommon.ConnectTimeLineKind.Local
            };
            
            var connection = WebSocketTimeLineCommon.CreateInstance(kind);
            if (connection != null)
            {
                connection.TimeLineDataReceived += OnPersistentTimeLineDataReceived;
                
                var apiKey = _instanceTokens.ContainsKey(instanceName) ? _instanceTokens[instanceName] : null;
                
                await Task.Run(() =>
                {
                    try
                    {
                        connection.OpenTimeLine(instanceName, apiKey);
                        WebSocketTimeLineCommon.ReadTimeLineContinuous(connection);
                        
                        _persistentConnections[instanceName][timelineType] = connection;
                        Console.WriteLine($"Reconnected: {instanceName} - {timelineType}");
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Reconnection failed: {instanceName} - {timelineType}: {ex.Message}");
                    }
                });
            }
        }
    }
    
    public class AppSettings
    {
        public List<string> Instances { get; set; } = new List<string>();
        public Dictionary<string, string> InstanceTokens { get; set; } = new Dictionary<string, string>();
    }
}