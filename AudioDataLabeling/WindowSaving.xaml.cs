using NAudio.Wave;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;

namespace AudioDataLabeling
{
    public partial class WindowSaving : Window
    {
        private ObservableCollection<TrackItem> _musicList;
        private AppSettings _appSettings;

        private CancellationTokenSource _cts = new CancellationTokenSource();
        private bool _isSavingActive = false;

        public bool IsSaveSuccessful { get; private set; } = false;

        public WindowSaving(ObservableCollection<TrackItem> musicList, AppSettings appSettings)
        {
            InitializeComponent();
            _musicList = musicList;
            _appSettings = appSettings;

            LoadProgressBar.Value = 0;
            TextProgressBar.Text = string.Empty;

            BtnOk.IsEnabled = false;
            BtnOk.Click += (s, e) => this.DialogResult = true;

            this.Closing += WindowSaving_Closing;

            _ = SaveProcess();
        }

        private async Task SaveProcess()
        {
            _isSavingActive = true;
            CancellationToken token = _cts.Token;

            try
            {
                var tracksToSave = _musicList.Where(track => track.IsDone && !string.IsNullOrEmpty(track.AssignedLabel)).ToList();

                if (tracksToSave.Count == 0)
                {
                    TextProgressBar.Text = "No audio to save!";
                    BtnOk.IsEnabled = true;
                    _isSavingActive = false;
                    return;
                }

                LoadProgressBar.Minimum = 0;
                LoadProgressBar.Maximum = tracksToSave.Count;
                LoadProgressBar.Value = 0;
                TextProgressBar.Text = "Saving... 0%";

                IProgress<int> progressHandler = new Progress<int>(currentValue =>
                {
                    LoadProgressBar.Value = currentValue;
                    double percent = ((double)currentValue / tracksToSave.Count) * 100;
                    TextProgressBar.Text = $"Saving... {Math.Round(percent, 0)}%";
                });

                IProgress<string> logHandler = new Progress<string>(logLine =>
                {
                    if (this.IsLoaded)
                    {
                        SavingLogs.AppendText(logLine + Environment.NewLine);
                        SavingLogs.ScrollToEnd();
                    }
                });

                string savePath = _appSettings.JSONfilePath;

                await Task.Run(() =>
                {
                    var jsonOptions = new JsonSerializerOptions
                    {
                        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
                    };

                    List<string> linesToWrite = new List<string>();
                    int savedCounter = 0;

                    foreach (var track in tracksToSave)
                    {
                        if (token.IsCancellationRequested)
                        {
                            break;
                        }

                        double duration = 0;
                        try
                        {
                            using (var tempReader = new AudioFileReader(track.FilePath))
                            {
                                duration = Math.Round(tempReader.TotalTime.TotalSeconds, 3);
                            }
                        }
                        catch { }

                        AudioAnnotation annotation = new AudioAnnotation
                        {
                            AudioFile = track.FileName,
                            Category = track.AssignedLabel,
                            Duration = duration
                        };

                        linesToWrite.Add(JsonSerializer.Serialize(annotation, jsonOptions));
                        savedCounter++;

                        progressHandler.Report(savedCounter);
                        logHandler.Report($"[{savedCounter}/{tracksToSave.Count}] Saved: {annotation.AudioFile} -> {annotation.Category} ({annotation.Duration}s)");
                    }

                    if (!token.IsCancellationRequested)
                    {
                        //File.WriteAllLines(savePath, linesToWrite);
                        string tempPath = savePath + ".tmp";
                        string backupPath = savePath + ".bak";

                        File.WriteAllLines(tempPath, linesToWrite);

                        if (File.Exists(savePath))
                        {
                            if (File.Exists(backupPath))
                            {
                                File.Delete(backupPath);
                            }

                            File.Replace(tempPath, savePath, backupPath);
                        }
                        else
                        {
                            File.Move(tempPath, savePath);
                        }
                    }
                }, token);

                if (!token.IsCancellationRequested)
                {
                    TextProgressBar.Text = "Complete!";
                    IsSaveSuccessful = true;
                    BtnOk.IsEnabled = true;

                }
                else
                {
                    TextProgressBar.Text = "Saving cancelled!";
                }
            }
            catch (Exception ex)
            {
                SavingLogs.Text = $"Save Error: {ex.Message}";
            }
            finally
            {
                _isSavingActive = false;
                BtnOk.IsEnabled = true;
            }
        }
        private async void WindowSaving_Closing(object sender, CancelEventArgs e)
        {
            if (_isSavingActive)
            {
                e.Cancel = true;

                TextProgressBar.Text = "Cancelling... Please wait.";
                _cts.Cancel();

                while (_isSavingActive)
                {
                    await Task.Delay(50);
                }
                this.Closing -= WindowSaving_Closing;
                this.Close();
            }
        }
    }
}
