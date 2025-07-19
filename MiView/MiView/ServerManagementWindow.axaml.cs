using Avalonia.Controls;
using Avalonia.Interactivity;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace MiView
{
    public partial class ServerManagementWindow : Window
    {
        private readonly ObservableCollection<string> _instances;
        private readonly Dictionary<string, string> _instanceTokens;
        private readonly ObservableCollection<ServerInfo> _serverList = new();
        private bool _hasChanges = false;
        private const string SETTINGS_FILE = "settings.json";

        public ServerManagementWindow(ObservableCollection<string> instances, Dictionary<string, string> instanceTokens)
        {
            InitializeComponent();
            _instances = instances;
            _instanceTokens = instanceTokens;
            
            serverListBox.ItemsSource = _serverList;
            LoadServerList();
        }

        private void LoadServerList()
        {
            _serverList.Clear();
            foreach (var instance in _instances)
            {
                _serverList.Add(new ServerInfo
                {
                    InstanceName = instance,
                    HasApiKey = _instanceTokens.ContainsKey(instance) && !string.IsNullOrEmpty(_instanceTokens[instance])
                });
            }
        }

        private async void DeleteServer(object? sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.DataContext is ServerInfo serverInfo)
            {
                var result = await ShowConfirmDialog($"サーバー '{serverInfo.InstanceName}' を削除しますか？");
                if (result)
                {
                    // インスタンスリストから削除
                    _instances.Remove(serverInfo.InstanceName);
                    
                    // トークンも削除
                    if (_instanceTokens.ContainsKey(serverInfo.InstanceName))
                    {
                        _instanceTokens.Remove(serverInfo.InstanceName);
                    }
                    
                    // 設定ファイルから削除
                    RemoveFromSettings(serverInfo.InstanceName);
                    
                    // UI更新
                    _serverList.Remove(serverInfo);
                    _hasChanges = true;
                }
            }
        }

        private async void EditServer(object? sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.DataContext is ServerInfo serverInfo)
            {
                // 編集ダイアログを表示（簡易版）
                var editWindow = new ServerEditWindow(serverInfo.InstanceName, 
                    _instanceTokens.ContainsKey(serverInfo.InstanceName) ? _instanceTokens[serverInfo.InstanceName] : "");
                
                var result = await editWindow.ShowDialog<ServerEditResult?>(this);
                if (result?.Success == true && result.ApiKey != null)
                {
                    _instanceTokens[serverInfo.InstanceName] = result.ApiKey;
                    SaveSettings();
                    LoadServerList();
                    _hasChanges = true;
                }
            }
        }

        private async void AddServer(object? sender, RoutedEventArgs e)
        {
            var addWindow = new ServerEditWindow("", "")
            {
                Title = "サーバー追加"
            };
            
            var result = await addWindow.ShowDialog<ServerEditResult?>(this);
            if (result?.Success == true && !string.IsNullOrEmpty(result.InstanceName))
            {
                if (!_instances.Contains(result.InstanceName))
                {
                    _instances.Add(result.InstanceName);
                    if (!string.IsNullOrEmpty(result.ApiKey))
                    {
                        _instanceTokens[result.InstanceName] = result.ApiKey;
                    }
                    SaveSettings();
                    LoadServerList();
                    _hasChanges = true;
                }
            }
        }

        private void CloseWindow(object? sender, RoutedEventArgs e)
        {
            Close(_hasChanges);
        }

        private void RemoveFromSettings(string instanceName)
        {
            try
            {
                if (File.Exists(SETTINGS_FILE))
                {
                    var jsonString = File.ReadAllText(SETTINGS_FILE);
                    var settings = JsonSerializer.Deserialize<Dictionary<string, object>>(jsonString) ?? new();
                    
                    var keyToRemove = $"instance_{instanceName}";
                    if (settings.ContainsKey(keyToRemove))
                    {
                        settings.Remove(keyToRemove);
                    }
                    
                    File.WriteAllText(SETTINGS_FILE, JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true }));
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error removing from settings: {ex.Message}");
            }
        }

        private void SaveSettings()
        {
            try
            {
                var settings = new Dictionary<string, object>();
                
                // 既存設定を読み込み
                if (File.Exists(SETTINGS_FILE))
                {
                    var jsonString = File.ReadAllText(SETTINGS_FILE);
                    settings = JsonSerializer.Deserialize<Dictionary<string, object>>(jsonString) ?? new();
                }
                
                // インスタンス設定を更新
                foreach (var instance in _instances)
                {
                    var key = $"instance_{instance}";
                    if (_instanceTokens.ContainsKey(instance))
                    {
                        settings[key] = _instanceTokens[instance];
                    }
                    else
                    {
                        settings[key] = "";
                    }
                }
                
                File.WriteAllText(SETTINGS_FILE, JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true }));
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error saving settings: {ex.Message}");
            }
        }

        private async Task<bool> ShowConfirmDialog(string message)
        {
            var dialog = new Window
            {
                Title = "確認",
                Width = 300,
                Height = 150,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Background = Avalonia.Media.Brush.Parse("#F0F0F0")
            };

            var panel = new StackPanel { Margin = new Avalonia.Thickness(20), Background = Avalonia.Media.Brushes.Transparent };
            panel.Children.Add(new TextBlock { 
                Text = message, 
                Margin = new Avalonia.Thickness(0, 0, 0, 20), 
                Foreground = Avalonia.Media.Brush.Parse("#000000"),
                Background = Avalonia.Media.Brushes.Transparent
            });

            var buttonPanel = new StackPanel { 
                Orientation = Avalonia.Layout.Orientation.Horizontal, 
                HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Right,
                Background = Avalonia.Media.Brushes.Transparent
            };
            
            var yesButton = new Button { 
                Content = "はい", 
                Margin = new Avalonia.Thickness(0, 0, 10, 0), 
                Background = Avalonia.Media.Brush.Parse("#FFFFFF"), 
                Foreground = Avalonia.Media.Brush.Parse("#000000"),
                BorderBrush = Avalonia.Media.Brush.Parse("#8C8C8C"),
                BorderThickness = new Avalonia.Thickness(1),
                Padding = new Avalonia.Thickness(15, 5)
            };
            var noButton = new Button { 
                Content = "いいえ", 
                Background = Avalonia.Media.Brush.Parse("#FFFFFF"), 
                Foreground = Avalonia.Media.Brush.Parse("#000000"),
                BorderBrush = Avalonia.Media.Brush.Parse("#8C8C8C"),
                BorderThickness = new Avalonia.Thickness(1),
                Padding = new Avalonia.Thickness(15, 5)
            };

            bool result = false;
            yesButton.Click += (s, e) => { result = true; dialog.Close(); };
            noButton.Click += (s, e) => { result = false; dialog.Close(); };

            buttonPanel.Children.Add(yesButton);
            buttonPanel.Children.Add(noButton);
            panel.Children.Add(buttonPanel);

            dialog.Content = panel;
            await dialog.ShowDialog(this);
            return result;
        }
    }

    public class ServerInfo
    {
        public string InstanceName { get; set; } = "";
        public bool HasApiKey { get; set; }
        public string ApiKeyStatus => HasApiKey ? "APIキー: 設定済み" : "APIキー: 未設定";
    }

    public class ServerEditResult
    {
        public bool Success { get; set; }
        public string? InstanceName { get; set; }
        public string? ApiKey { get; set; }
    }
}