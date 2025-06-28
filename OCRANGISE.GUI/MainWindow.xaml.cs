using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Threading;
using Microsoft.Win32;
using OCRANGISE.Core.Pipeline;
using OCRANGISE.Core.Models;

namespace OCRANGISE.GUI
{
    public partial class MainWindow : Window, INotifyPropertyChanged
    {
        private ProcessingPipeline? _pipeline;
        private ObservableCollection<string> _monitoredFolders = new();
        private ObservableCollection<ActivityLogItem> _activityLog = new();
        private DispatcherTimer? _statisticsTimer;

        // Properties for data binding
        private int _processedCount = 0;
        public int ProcessedCount
        {
            get => _processedCount;
            set
            {
                if (_processedCount != value)
                {
                    _processedCount = value;
                    OnPropertyChanged();
                }
            }
        }

        private int _failedCount = 0;
        public int FailedCount
        {
            get => _failedCount;
            set
            {
                if (_failedCount != value)
                {
                    _failedCount = value;
                    OnPropertyChanged();
                }
            }
        }

        private int _activeRulesCount = 1;
        public int ActiveRulesCount
        {
            get => _activeRulesCount;
            set
            {
                if (_activeRulesCount != value)
                {
                    _activeRulesCount = value;
                    OnPropertyChanged();
                }
            }
        }

        private string _currentStatus = "Ready";
        public string CurrentStatus
        {
            get => _currentStatus;
            set
            {
                if (_currentStatus != value)
                {
                    _currentStatus = value;
                    OnPropertyChanged();
                }
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        public MainWindow()
        {
            InitializeComponent();
            DataContext = this;
            InitializeData();
            SetupEventHandlers();
            StartStatisticsTimer();
        }

        private void InitializeData()
        {
            FolderListBox.ItemsSource = _monitoredFolders;
            ActivityListView.ItemsSource = _activityLog;

            _monitoredFolders.Add(@"D:\Documents\Scanned");
            _monitoredFolders.Add(@"D:\Inbox");
        }

        private void SetupEventHandlers()
        {
            Loaded += (s, e) => InitializePipeline();
        }

        private void StartStatisticsTimer()
        {
            _statisticsTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(500)
            };
            _statisticsTimer.Tick += UpdateStatisticsFromTimer;
            _statisticsTimer.Start();
        }

        private void UpdateStatisticsFromTimer(object? sender, EventArgs e)
        {
            if (_pipeline != null)
            {
                ProcessedCount = _pipeline.ProcessedFilesCount;
                FailedCount = _pipeline.FailedFilesCount;
                ActiveRulesCount = _pipeline.GetRules().Count(r => r.IsActive);
            }
        }

        private void InitializePipeline()
        {
            try
            {
                _pipeline = new ProcessingPipeline(this.Dispatcher);

                _pipeline.FileProcessed += OnFileProcessed;
                _pipeline.ProcessingFailed += OnProcessingFailed;

                AddActivityLogEntry("✅", "System initialized", "OCRANGISE ready to process files");
                UpdateStatus("Ready", Colors.Green);
            }
            catch (Exception ex)
            {
                AddActivityLogEntry("❌", "Initialization failed", ex.Message);
                UpdateStatus("Error", Colors.Red);
            }
        }

        private void OnFileProcessed(string originalPath, string newPath)
        {
            Dispatcher.Invoke(() =>
            {
                ProcessedCount = _pipeline?.ProcessedFilesCount ?? 0;

                var originalName = Path.GetFileName(originalPath);
                var newName = Path.GetFileName(newPath);

                AddActivityLogEntry("✅", "File processed", $"{originalName} → {newName}");
                ShowToastNotification(originalName, newName);
            });
        }

        private void OnProcessingFailed(string filePath, string error)
        {
            Dispatcher.Invoke(() =>
            {
                FailedCount = _pipeline?.FailedFilesCount ?? 0;
                AddActivityLogEntry("❌", "Processing failed", $"{Path.GetFileName(filePath)}: {error}");
            });
        }

        #region Button Event Handlers

        private void AddFolderBtn_Click(object sender, RoutedEventArgs e)
        {
            using var dialog = new System.Windows.Forms.FolderBrowserDialog();
            var result = dialog.ShowDialog();

            if (result == System.Windows.Forms.DialogResult.OK)
            {
                if (!_monitoredFolders.Contains(dialog.SelectedPath))
                {
                    _monitoredFolders.Add(dialog.SelectedPath);
                    AddActivityLogEntry("📁", "Folder added", dialog.SelectedPath);
                }
            }
        }

        private void RemoveFolderBtn_Click(object sender, RoutedEventArgs e)
        {
            if (FolderListBox.SelectedItem is string selectedFolder)
            {
                _monitoredFolders.Remove(selectedFolder);
                AddActivityLogEntry("🗑️", "Folder removed", selectedFolder);
            }
        }

        private async void StartBtn_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (_pipeline != null && _monitoredFolders?.Any() == true)
                {
                    await Task.Run(() => _pipeline.StartMonitoring(_monitoredFolders.ToArray()));

                    StartBtn.IsEnabled = false;
                    StopBtn.IsEnabled = true;
                    UpdateStatus("Monitoring", Colors.Orange);
                    CurrentStatus = "Monitoring";

                    AddActivityLogEntry("▶️", "Monitoring started", $"Watching {_monitoredFolders.Count} folders");
                }
            }
            catch (Exception ex)
            {
                AddActivityLogEntry("❌", "Failed to start monitoring", ex.Message);
            }
        }

        private async void StopBtn_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (_pipeline != null)
                {
                    await Task.Run(() => _pipeline.StopMonitoring());
                }

                StartBtn.IsEnabled = true;
                StopBtn.IsEnabled = false;
                UpdateStatus("Stopped", Colors.Gray);
                CurrentStatus = "Stopped";

                AddActivityLogEntry("⏹️", "Monitoring stopped", "File monitoring has been stopped");
            }
            catch (Exception ex)
            {
                AddActivityLogEntry("❌", "Failed to stop monitoring", ex.Message);
            }
        }

        private async void TestFileBtn_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new OpenFileDialog
            {
                Filter = "Image files (*.jpg;*.png;*.tiff)|*.jpg;*.png;*.tiff|PDF files (*.pdf)|*.pdf",
                Title = "Select a file to test OCR processing"
            };

            if (dialog.ShowDialog() == true)
            {
                try
                {
                    AddActivityLogEntry("🧪", "Testing file", Path.GetFileName(dialog.FileName));

                    await Task.Run(() => _pipeline?.ProcessFileManually(dialog.FileName));

                    AddActivityLogEntry("✅", "Test completed", Path.GetFileName(dialog.FileName));
                }
                catch (Exception ex)
                {
                    AddActivityLogEntry("❌", "Test failed", ex.Message);
                }
            }
        }

        private void ViewLogsBtn_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var logsPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "logs");
                if (Directory.Exists(logsPath))
                {
                    System.Diagnostics.Process.Start("explorer.exe", logsPath);
                }
                else
                {
                    MessageBox.Show("Logs folder not found.", "Information",
                                  MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                AddActivityLogEntry("❌", "Failed to open logs", ex.Message);
            }
        }

        private void RefreshLogsBtn_Click(object sender, RoutedEventArgs e)
        {
            AddActivityLogEntry("🔄", "Logs refreshed", "Activity log has been updated");
        }

        #endregion

        #region Activity Log Methods (FIXED)

        private void ShowActivityLog(object sender, RoutedEventArgs e)
        {
            // Activity log is already visible in the main interface
            MessageBox.Show("Activity log is displayed in the right panel.", "Activity Log",
                           MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void HideActivityLog(object sender, RoutedEventArgs e)
        {
            // No overlay to hide since activity log is integrated in main UI
            // Method kept for compatibility
        }

        #endregion

        #region Toast Notification

        private void ShowToastNotification(string originalName, string newName)
        {
            if (!Dispatcher.CheckAccess())
            {
                Dispatcher.Invoke(() => ShowToastNotification(originalName, newName));
                return;
            }

            var toast = new Window
            {
                Width = 350,
                Height = 100,
                WindowStyle = WindowStyle.None,
                AllowsTransparency = true,
                Background = Brushes.Transparent,
                Topmost = true,
                ShowInTaskbar = false
            };

            toast.Left = SystemParameters.WorkArea.Right - toast.Width - 20;
            toast.Top = SystemParameters.WorkArea.Bottom - toast.Height - 20;

            var border = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(76, 175, 80)),
                CornerRadius = new CornerRadius(8),
                Padding = new Thickness(15),
                Effect = new DropShadowEffect
                {
                    Color = Colors.Black,
                    Direction = 270,
                    ShadowDepth = 3,
                    Opacity = 0.3
                }
            };

            var stackPanel = new StackPanel();

            var titleText = new TextBlock
            {
                Text = "✅ File Renamed Successfully",
                Foreground = Brushes.White,
                FontWeight = FontWeights.Bold,
                FontSize = 14
            };

            var detailText = new TextBlock
            {
                Text = $"{originalName} → {newName}",
                Foreground = Brushes.White,
                FontSize = 11,
                Margin = new Thickness(0, 5, 0, 0),
                TextTrimming = TextTrimming.CharacterEllipsis
            };

            stackPanel.Children.Add(titleText);
            stackPanel.Children.Add(detailText);
            border.Child = stackPanel;
            toast.Content = border;

            var slideIn = new System.Windows.Media.Animation.DoubleAnimation
            {
                From = SystemParameters.WorkArea.Right,
                To = SystemParameters.WorkArea.Right - toast.Width - 20,
                Duration = TimeSpan.FromMilliseconds(300)
            };

            toast.Show();
            toast.BeginAnimation(Window.LeftProperty, slideIn);

            var timer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(4)
            };
            timer.Tick += (s, e) =>
            {
                timer.Stop();

                var slideOut = new System.Windows.Media.Animation.DoubleAnimation
                {
                    From = toast.Left,
                    To = SystemParameters.WorkArea.Right,
                    Duration = TimeSpan.FromMilliseconds(300)
                };
                slideOut.Completed += (sender, args) => toast.Close();
                toast.BeginAnimation(Window.LeftProperty, slideOut);
            };
            timer.Start();

            toast.MouseLeftButtonDown += (s, e) =>
            {
                timer.Stop();
                toast.Close();
            };
        }

        #endregion

        #region Helper Methods

        private void UpdateStatus(string status, Color color)
        {
            if (!Dispatcher.CheckAccess())
            {
                Dispatcher.Invoke(() => UpdateStatus(status, color));
                return;
            }

            StatusText.Text = status;
            StatusIndicator.Fill = new SolidColorBrush(color);
            StatusBarText.Text = $"Status: {status} - {DateTime.Now:HH:mm:ss}";
            CurrentStatus = status;
        }

        private void AddActivityLogEntry(string icon, string message, string details)
        {
            if (!Dispatcher.CheckAccess())
            {
                Dispatcher.Invoke(() => AddActivityLogEntry(icon, message, details));
                return;
            }

            _activityLog.Insert(0, new ActivityLogItem
            {
                Icon = icon,
                Message = message,
                Details = details,
                Time = DateTime.Now.ToString("HH:mm:ss")
            });

            while (_activityLog.Count > 100)
            {
                _activityLog.RemoveAt(_activityLog.Count - 1);
            }
        }

        #endregion

        protected override void OnClosed(EventArgs e)
        {
            _statisticsTimer?.Stop();
            _pipeline?.Dispose();
            base.OnClosed(e);
        }
    }

    public class ActivityLogItem : INotifyPropertyChanged
    {
        private string _icon = "";
        public string Icon
        {
            get => _icon;
            set
            {
                if (_icon != value)
                {
                    _icon = value;
                    OnPropertyChanged();
                }
            }
        }

        private string _message = "";
        public string Message
        {
            get => _message;
            set
            {
                if (_message != value)
                {
                    _message = value;
                    OnPropertyChanged();
                }
            }
        }

        private string _details = "";
        public string Details
        {
            get => _details;
            set
            {
                if (_details != value)
                {
                    _details = value;
                    OnPropertyChanged();
                }
            }
        }

        private string _time = "";
        public string Time
        {
            get => _time;
            set
            {
                if (_time != value)
                {
                    _time = value;
                    OnPropertyChanged();
                }
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
