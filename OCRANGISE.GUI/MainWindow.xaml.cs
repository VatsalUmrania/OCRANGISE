using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Media;
using Microsoft.Win32;
using OCRANGISE.Core.Pipeline;
using OCRANGISE.Core.Models;

namespace OCRANGISE.GUI
{
    public partial class MainWindow : Window
    {
        private ProcessingPipeline? _pipeline;
        private ObservableCollection<string>? _monitoredFolders; // Nullable declaration
        private ObservableCollection<ActivityLogItem>? _activityLog; // Nullable declaration

        public MainWindow()
        {
            InitializeComponent();
            InitializeData();
            SetupEventHandlers();
        }

        private void InitializeData()
        {
            _monitoredFolders = new ObservableCollection<string>(); // Initialize here
            _activityLog = new ObservableCollection<ActivityLogItem>(); // Initialize here

            FolderListBox.ItemsSource = _monitoredFolders;
            ActivityListView.ItemsSource = _activityLog;

            // Add sample folders
            _monitoredFolders.Add(@"D:\Documents\Scanned");
            _monitoredFolders.Add(@"D:\Inbox");
        }

        private void SetupEventHandlers()
        {
            // Initialize pipeline when window loads
            Loaded += (s, e) => InitializePipeline();
        }

        private void InitializePipeline()
        {
            try
            {
                _pipeline = new ProcessingPipeline(); // This connects to your Core backend
                AddActivityLog("✅", "System initialized", "OCRANGISE ready to process files");
                UpdateStatus("Ready", Colors.Green);
            }
            catch (Exception ex)
            {
                AddActivityLog("❌", "Initialization failed", ex.Message);
                UpdateStatus("Error", Colors.Red);
            }
        }


        #region Button Event Handlers

        private void AddFolderBtn_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new System.Windows.Forms.FolderBrowserDialog();
            if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                if (!_monitoredFolders.Contains(dialog.SelectedPath))
                {
                    _monitoredFolders.Add(dialog.SelectedPath);
                    AddActivityLog("📁", "Folder added", dialog.SelectedPath);
                }
            }
        }

        private void RemoveFolderBtn_Click(object sender, RoutedEventArgs e)
        {
            if (FolderListBox.SelectedItem is string selectedFolder)
            {
                _monitoredFolders.Remove(selectedFolder);
                AddActivityLog("🗑️", "Folder removed", selectedFolder);
            }
        }

        private void StartBtn_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (_pipeline != null && _monitoredFolders.Any())
                {
                    _pipeline.StartMonitoring(_monitoredFolders.ToArray());

                    StartBtn.IsEnabled = false;
                    StopBtn.IsEnabled = true;
                    UpdateStatus("Monitoring", Colors.Orange);

                    AddActivityLog("▶️", "Monitoring started", $"Watching {_monitoredFolders.Count} folders");
                }
            }
            catch (Exception ex)
            {
                AddActivityLog("❌", "Failed to start monitoring", ex.Message);
            }
        }

        private void StopBtn_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                _pipeline?.StopMonitoring();

                StartBtn.IsEnabled = true;
                StopBtn.IsEnabled = false;
                UpdateStatus("Stopped", Colors.Gray);

                AddActivityLog("⏹️", "Monitoring stopped", "File monitoring has been stopped");
            }
            catch (Exception ex)
            {
                AddActivityLog("❌", "Failed to stop monitoring", ex.Message);
            }
        }

        private void TestFileBtn_Click(object sender, RoutedEventArgs e)
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
                    _pipeline?.ProcessFileManually(dialog.FileName);
                    AddActivityLog("🧪", "Test file processed", Path.GetFileName(dialog.FileName));
                }
                catch (Exception ex)
                {
                    AddActivityLog("❌", "Test failed", ex.Message);
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
                AddActivityLog("❌", "Failed to open logs", ex.Message);
            }
        }

        private void AddRuleBtn_Click(object sender, RoutedEventArgs e)
        {
            // TODO: Open rule editor dialog
            MessageBox.Show("Rule editor coming soon!", "Information",
                          MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void RefreshLogsBtn_Click(object sender, RoutedEventArgs e)
        {
            // Refresh activity log
            AddActivityLog("🔄", "Logs refreshed", "Activity log has been updated");
        }

        #endregion

        #region Helper Methods

        private void UpdateStatus(string status, Color color)
        {
            StatusText.Text = status;
            StatusIndicator.Fill = new SolidColorBrush(color);
            StatusBarText.Text = $"Status: {status} - {DateTime.Now:HH:mm:ss}";
        }

        private void AddActivityLog(string icon, string message, string details)
        {
            _activityLog.Insert(0, new ActivityLogItem
            {
                Icon = icon,
                Message = message,
                Details = details,
                Time = DateTime.Now.ToString("HH:mm:ss")
            });

            // Keep only last 50 items
            while (_activityLog.Count > 50)
            {
                _activityLog.RemoveAt(_activityLog.Count - 1);
            }
        }

        #endregion

        protected override void OnClosed(EventArgs e)
        {
            _pipeline?.Dispose();
            base.OnClosed(e);
        }
    }

    public class ActivityLogItem
    {
        public string Icon { get; set; } = "";
        public string Message { get; set; } = "";
        public string Details { get; set; } = "";
        public string Time { get; set; } = "";
    }
}
