using Microsoft.Win32;
using NAudio.Wave;
using System;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Forms;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace AudioDataLabeling
{
    public class AppSettings
    {
        public string AudioFolderPath { get; set; } = string.Empty;
        public int NumberOfLoops { get; set; } = 3;
        public int AudioDeviceID { get; set; } = -1;
        public int AudioLatency { get; set; } = 100;
        public string JSONfilePath { get; set; } = string.Empty;
        public bool MultiThreadedRendering {  get; set; } = true;
    }
    public class AudioDeviceItem
    {
        public int Id { get; set; }
        public string Name { get; set; }
    }

    public partial class WindowSettings : Window
    {
        public AppSettings Settings { get; private set; }
        private ObservableCollection<AudioDeviceItem> _AudioDevices = new ObservableCollection<AudioDeviceItem>();

        public WindowSettings(AppSettings currentSettings)
        {
            InitializeComponent();
            Settings = new AppSettings
            {
                AudioFolderPath = currentSettings.AudioFolderPath,
                NumberOfLoops = currentSettings.NumberOfLoops,
                AudioDeviceID = currentSettings.AudioDeviceID,
                AudioLatency = currentSettings.AudioLatency,
                JSONfilePath = currentSettings.JSONfilePath,
                MultiThreadedRendering = currentSettings.MultiThreadedRendering
            };

            NumberOfLoops_Slider.Value = Settings.NumberOfLoops;
            Latency_Slider.Value = Settings.AudioLatency;
            JSONfilePath_Field.Text = Settings.JSONfilePath;
            AudioFolder_TextBox.Text = Settings.AudioFolderPath;
            MuliTreadedRenderingWF.IsChecked = Settings.MultiThreadedRendering;

            GetWaveOutDevices();

            Devices_ComboBox.ItemsSource = _AudioDevices;
            var currentDevice = _AudioDevices.FirstOrDefault(d => d.Id == Settings.AudioDeviceID);
            if (currentDevice != null)
            {
                Devices_ComboBox.SelectedItem = currentDevice;
            }

        }
        private void SaveAndExit_Click(object sender, RoutedEventArgs e)
        {
            Settings.NumberOfLoops = (int)NumberOfLoops_Slider.Value;
            Settings.AudioLatency = (int)Latency_Slider.Value;
            Settings.JSONfilePath = JSONfilePath_Field.Text;
            Settings.MultiThreadedRendering = MuliTreadedRenderingWF.IsChecked ?? false;
            if (Devices_ComboBox.SelectedValue != null)
            {
                Settings.AudioDeviceID = (int)Devices_ComboBox.SelectedValue;
            }
            DialogResult = true;
            Close();
        }

        public void GetWaveOutDevices()
        {
            _AudioDevices.Add(new AudioDeviceItem { Id = -1, Name = "Устройство по умолчанию" });

            int deviceCount = WaveOut.DeviceCount;

            for (int i = 0; i < deviceCount; i++)
            {
                WaveOutCapabilities caps = WaveOut.GetCapabilities(i);

                _AudioDevices.Add(new AudioDeviceItem
                {
                    Id = i,
                    Name = caps.ProductName
                });
            }
        }

        private void ImportJSON_Click(object sender, RoutedEventArgs e)
        {
            Microsoft.Win32.OpenFileDialog openFileDialog = new Microsoft.Win32.OpenFileDialog();
            openFileDialog.Filter = "JSON Lines file (*.jsonl)|*.jsonl|Text file (*.txt)|*.txt";
            openFileDialog.Title = "Import existing JSON";
            if (openFileDialog.ShowDialog() == true)
            {
                JSONfilePath_Field.Text = openFileDialog.FileName;
            }
            JSONfilePath_Field.Tag = null;
            ErrorMessageLabel.Visibility = Visibility.Collapsed;
        }

        private void CreateJSONfile_Click(object sender, RoutedEventArgs e)
        {
            Microsoft.Win32.SaveFileDialog saveFileDialog = new Microsoft.Win32.SaveFileDialog();
            saveFileDialog.Filter = "JSON Lines file (*.jsonl)|*.jsonl";
            saveFileDialog.Title = "Create JSON file";
            saveFileDialog.FileName = "datasetX.jsonl";
            if (saveFileDialog.ShowDialog() == true)
            {
                JSONfilePath_Field.Text = saveFileDialog.FileName;
            }
            JSONfilePath_Field.Tag = null;
            ErrorMessageLabel.Visibility = Visibility.Collapsed;
        }

        private void ShowInExplorer_Click(object sender, RoutedEventArgs e)
        {
            if (File.Exists(JSONfilePath_Field.Text))
            {
                Process.Start("explorer.exe", $"/select, \"{JSONfilePath_Field.Text}\"");
            }
        }
    }
}
