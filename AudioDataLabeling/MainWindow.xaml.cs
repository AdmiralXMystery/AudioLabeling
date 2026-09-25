using Microsoft.Win32;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;
using System.Collections.ObjectModel;
using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace AudioDataLabeling
{
    public class AudioAnnotation
    {
        public required string AudioFile { get; set; }
        public required string Category { get; set; }
        public double Duration { get; set; }
    }
    public partial class MainWindow : Window
    {
        private ObservableCollection<TrackItem> _musicList = new ObservableCollection<TrackItem>();

        private string[] _audioFormats = { ".mp3", ".wav", ".aac", ".m4a" };

        private WaveOutEvent outputDevice;
        private MixingSampleProvider mixer;
        private ISampleProvider _currentSampleProvider;

        private AppSettings _appSettings = new AppSettings();

        private DispatcherTimer timer = new DispatcherTimer();
        private bool _isUpdatingByTimer = false;
        private readonly WaveFormat _targetFormat = WaveFormat.CreateIeeeFloatWaveFormat(44100, 2);

        private List<string> _buttonNames = new List<string> { "Button 1", "Button 2", "Button 3", "Button 4" };

        private int _currentLoopCount = 0;
        private bool _isShowWaveForm = false;
        private bool _hasUnsavedChanges = false;
        private CancellationTokenSource _cts;

        public MainWindow()
        {
            InitializeComponent();

            MusicListBox.ItemsSource = _musicList;
            timer.Interval = TimeSpan.FromMilliseconds(10);
            timer.Tick += Timer_Tick;
            this.Closing += MainWindow_Closing;

            mixer = new MixingSampleProvider(_targetFormat) { ReadFully = true };
            
            CreateButtons();
            InitAudioDevice();
        }

        private void MenuOpenFolder_Click(object sender, RoutedEventArgs e)
        {
            if (_hasUnsavedChanges)
            {
                var result = MessageBox.Show(
                    "Current changes have not been saved. Open another folder and delete your progress?",
                    "Warning",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question);

                if (result == MessageBoxResult.No)
                {
                    return;
                }
            }

            OpenFolderDialog dialog = new OpenFolderDialog();
            dialog.Title = "Select folder path";
            //dialog.InitialDirectory = @"D:\";

            if (dialog.ShowDialog() == true)
            {
                _musicList.Clear();
                _appSettings.AudioFolderPath = dialog.FolderName;
                List<string> allFiles = new List<string>();

                foreach (string filePath in Directory.GetFiles(_appSettings.AudioFolderPath))
                {
                    if (_audioFormats.Contains(System.IO.Path.GetExtension(filePath).ToLower()))
                    {
                        allFiles.Add(filePath);
                    }
                }

                var trackItems = allFiles.Select(path => new TrackItem { FilePath = path, IsDone = false });
                _musicList = new ObservableCollection<TrackItem>(trackItems);
                MusicListBox.ItemsSource = _musicList;
                ButtonFieldInfo.Content = $"{0}/{_musicList.Count}";
                WaveformImage.Source = null;
            }
        }

        private void InitAudioDevice()
        {
            if (outputDevice != null)
            {
                outputDevice.Stop();
                outputDevice.Dispose();
                outputDevice = null;
            }

            outputDevice = new WaveOutEvent
            {
                DeviceNumber = _appSettings.AudioDeviceID,
                DesiredLatency = _appSettings.AudioLatency
            };

            outputDevice.Init(mixer);
            outputDevice.Volume = (float)VolumeSlider.Value;

            outputDevice.Play();
        }

        private void StartSelectedTrack()
        {
            if (MusicListBox.SelectedIndex == -1 && MusicListBox.Items.Count > 0)
            {
                MusicListBox.SelectedIndex = 0;
            }

            if (MusicListBox.SelectedItem == null) return;
            TrackItem selectedTrack = (TrackItem)MusicListBox.SelectedItem;

            MusicListBox.ScrollIntoView(selectedTrack);

            string filePath = selectedTrack.FilePath;

            _currentLoopCount = 0;

            timer.Stop();
            mixer.RemoveAllMixerInputs();

            if (_currentSampleProvider is IDisposable oldStream)
            {
                oldStream.Dispose();
            }
            try
            {
                var stream = new SafeTrackStreamProvider(filePath, _targetFormat);
                _currentSampleProvider = stream;
                if (_isShowWaveForm)
                {
                    _ = RenderWaveformAsync(filePath);
                }
                TimeLineSlider.Maximum = stream.TotalTime.TotalSeconds;
                RightTimeText.Text = stream.TotalTime.ToString(@"mm\:ss\.ff");
                TimeLineSlider.Value = 0;
                LeftTimeText.Text = "00:00.00";
                BtnPlayStop.Content = "◼";

                mixer.AddMixerInput(_currentSampleProvider);

                timer.Start();
                SetupMarqueeAnimation();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка запуска стриминга: {ex.Message}");
            }
        }

        private void TogglePlayPause()
        {
            if (_currentSampleProvider == null)
            {
                StartSelectedTrack();
                return;
            }
            if (timer.IsEnabled)
            {
                timer.Stop();
                mixer.RemoveAllMixerInputs();
                BtnPlayStop.Content = "▶";
            }
            else
            {
                if (_currentSampleProvider is SafeTrackStreamProvider stream)
                {
                    if (stream.TotalTime.TotalSeconds > 0 && stream.CurrentTime >= stream.TotalTime)
                    {
                        stream.CurrentTime = TimeSpan.Zero;
                    }
                }
                
                mixer.RemoveAllMixerInputs();
                mixer.AddMixerInput(_currentSampleProvider);
                timer.Start();
                BtnPlayStop.Content = "◼";
            }
        }

        private void PlayStop_Click(object sender, RoutedEventArgs e)
        {
            TogglePlayPause();
        }

        private void MusicListBox_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            StartSelectedTrack();
        }

        private void Timer_Tick(object sender, EventArgs e)
        {
            if (_currentSampleProvider is SafeTrackStreamProvider stream)
            {
                if (stream.TotalTime.TotalSeconds > 0 && stream.CurrentTime >= stream.TotalTime)
                {
                    if (BtnLoop.IsChecked == true && _currentLoopCount < _appSettings.NumberOfLoops)
                    {
                        _currentLoopCount++;

                        stream.CurrentTime = TimeSpan.Zero;

                        mixer.RemoveAllMixerInputs();
                        mixer.AddMixerInput(_currentSampleProvider);

                        _isUpdatingByTimer = true;
                        TimeLineSlider.Value = 0;
                        LeftTimeText.Text = "00:00.00";
                        _isUpdatingByTimer = false;

                        return;
                    }

                    timer.Stop();
                    PlayNextTrack();
                    return;
                }

                _isUpdatingByTimer = true;
                TimeLineSlider.Value = stream.CurrentTime.TotalSeconds;
                LeftTimeText.Text = stream.CurrentTime.ToString(@"mm\:ss\.ff");
                UpdatePlaybackMarker(stream.CurrentTime.TotalSeconds, stream.TotalTime.TotalSeconds);
                _isUpdatingByTimer = false;
            }
        }

        private void TimeLineSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (!_isUpdatingByTimer && _currentSampleProvider is SafeTrackStreamProvider stream)
            {
                stream.CurrentTime = TimeSpan.FromSeconds(TimeLineSlider.Value);
                LeftTimeText.Text = stream.CurrentTime.ToString(@"mm\:ss\.ff");
            }
        }

        private void VolumeSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (outputDevice != null) outputDevice.Volume = (float)VolumeSlider.Value;
        }

        private void ApplyFilter()
        {
            if (MusicListBox == null || MusicListBox.ItemsSource == null) return;
            string searchText = SearchFile_TextBox.Text.Trim().ToLower();

            var view = System.Windows.Data.CollectionViewSource.GetDefaultView(MusicListBox.ItemsSource);
            if (view == null) return;

            if (string.IsNullOrEmpty(searchText) || searchText.StartsWith("🔍"))
            {
                view.Filter = null;
                return;
            }

            view.Filter = obj =>
            {
                if (obj is TrackItem track)
                {
                    bool matchesName = track.FileName.ToLower().Contains(searchText);
                    bool matchesLabel = track.AssignedLabel.ToLower().Contains(searchText);

                    return matchesName || matchesLabel;
                }
                return false;
            };
        }

        private void SearchFile_TextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            ApplyFilter();
        }

        private void SearchFile_TextBox_GotFocus(object sender, RoutedEventArgs e)
        {
            if (SearchFile_TextBox.Text.StartsWith("🔍"))
            {
                SearchFile_TextBox.Text = string.Empty;
                SearchFile_TextBox.Foreground = new SolidColorBrush(Colors.Black);
            }
        }

        private void SearchFile_TextBox_LostFocus(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(SearchFile_TextBox.Text))
            {
                SearchFile_TextBox.Text = "🔍 Start typing to search by name or click the markup button...";
                SearchFile_TextBox.Foreground = new SolidColorBrush(Colors.Gray);
                ApplyFilter();
            }
        }

        private void MenuItemEditor_Click(object sender, RoutedEventArgs e)
        {
            WindowButtonEditor ButtonEditor = new WindowButtonEditor(_buttonNames);
            ButtonEditor.Owner = this;
            if (ButtonEditor.ShowDialog() == true)
            {
                _buttonNames = ButtonEditor.ResultButtonNames;
                CreateButtons();
            }
        }
        private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if ((Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control)
            {
                switch (e.Key)
                {
                    case Key.O: MenuOpenFolder_Click(null, null); e.Handled = true; return;
                    case Key.E:
                        WindowButtonEditor ButtonEditor = new WindowButtonEditor(_buttonNames);
                        ButtonEditor.Owner = this;
                        if (ButtonEditor.ShowDialog() == true)
                        {
                            _buttonNames = ButtonEditor.ResultButtonNames;
                            CreateButtons();
                        }
                        e.Handled = true;
                        return;
                    case Key.S: MenuSave_Click(null, null); e.Handled = true; return;
                    case Key.N: MenuItemSettings_Click(null, null); e.Handled = true; return;
                    case Key.F: SearchFile_TextBox.Focus(); e.Handled = true; return;
                    case Key.G: Vizialization_Click(null, null); e.Handled= true; return;
                    case Key.R: Reader_Click(null, null); e.Handled = true; return;
                }
            }

            if (!SearchFile_TextBox.IsKeyboardFocusWithin)
            {
                switch (e.Key)
                {
                    case Key.Space: TogglePlayPause(); e.Handled = true; break;
                    case Key.Left: PlayPreviousTrack(); e.Handled = true; break;
                    case Key.Right: PlayNextTrack(); e.Handled = true; break;
                }
            }
        }

        private void CreateButtons()
        {
            ButtonsContainer.Children.Clear();

            foreach (string name in _buttonNames)
            {
                Button btn = new Button
                {
                    Content = name,
                    Margin = new Thickness(3),
                    FontSize = 14,
                    //Background = new SolidColorBrush(Colors.White),
                    Focusable = false
                };

                btn.Style = (Style)this.FindResource("LabelButtonStyle");

                btn.Click += MainWindowButton_Click;
                btn.MouseRightButtonDown += MainWindowButton_RightClick;

                ButtonsContainer.Children.Add(btn);
            }
        }

        private void MainWindowButton_Click(object sender, RoutedEventArgs e)
        {
            if (MusicListBox.Items.Count == 0) return;

            if (sender is Button btn)
            {
                if (File.Exists(_appSettings.JSONfilePath))
                {
                    WriteJSON(btn);
                }
                else
                {
                    WindowSettings windowSettings = new WindowSettings(_appSettings);
                    windowSettings.JSONfilePath_Field.Tag = "Error";
                    windowSettings.ErrorMessageLabel.Text = "Select path for JSON file.";
                    windowSettings.ErrorMessageLabel.Visibility = Visibility.Visible;
                    if(timer.IsEnabled == true) TogglePlayPause();
                    if (windowSettings.ShowDialog() == true)
                    {
                        _appSettings = windowSettings.Settings;

                        if (!File.Exists(_appSettings.JSONfilePath))
                        {
                            System.IO.File.WriteAllText(_appSettings.JSONfilePath, string.Empty);
                        }
                        else
                        {
                            LoadLastSession();
                        }
                            
                        WriteJSON(btn);
                    }
                }
            }
        }

        private void MainWindowButton_RightClick(object sender, MouseButtonEventArgs e)
        {
            if (MusicListBox.Items.Count == 0) return;

            if (sender is Button btn)
            {
                string categoryName = btn.Content.ToString() ?? "";

                SearchFile_TextBox.Foreground = new SolidColorBrush(Colors.Black);

                SearchFile_TextBox.Text = categoryName;
                SearchFile_TextBox.SelectAll();
                SearchFile_TextBox.Focus();

                ApplyFilter();

                MusicListBox.Focus();

                e.Handled = true;
            }
        }

        public static string GetNextDefaultFileNameJSON(string folderPath)
        {
            string[] files = Directory.GetFiles(folderPath, "*.jsonl");
            return System.IO.Path.Join(folderPath, $"dataset{files.Length+1}.jsonl");
        }

        private void WriteJSON(Button button)
        {
            if (MusicListBox.SelectedItem == null) return;

            if (MusicListBox.SelectedItem is TrackItem currentTrack)
            {
                string labelName = button.Content.ToString() ?? "UnknownLabel";
                currentTrack.AssignedLabel = $"{labelName}";
                currentTrack.IsDone = true;
                _hasUnsavedChanges = true;
                PlayNextTrack();
            }
        }

        private void PlayNextTrack()
        {
            if (MusicListBox.Items.Count == 0) return;

            int currentIndex = MusicListBox.SelectedIndex;

            if (currentIndex == -1)
            {
                MusicListBox.SelectedIndex = 0;
                StartSelectedTrack();
                return;
            }

            int nextIndex = currentIndex + 1;

            if (nextIndex >= MusicListBox.Items.Count)
            {
                nextIndex = 0;
            }
            MusicListBox.SelectedIndex = nextIndex;
            StartSelectedTrack();
        }

        private void BtnBack_Click(object sender, RoutedEventArgs e)
        {
            PlayPreviousTrack();
        }

        private void BtnNext_Click(object sender, RoutedEventArgs e)
        {
            PlayNextTrack();
        }

        private void PlayPreviousTrack()
        {
            if (MusicListBox.Items.Count == 0) return;

            int currentIndex = MusicListBox.SelectedIndex;

            if (currentIndex == -1)
            {
                MusicListBox.SelectedIndex = 0;
                StartSelectedTrack();
                return;
            }

            int prevIndex = currentIndex - 1;

            if (prevIndex < 0)
            {
                prevIndex = MusicListBox.Items.Count - 1;
            }

            MusicListBox.SelectedIndex = prevIndex;
            StartSelectedTrack();
        }

        private void BtnClear_Click(object sender, RoutedEventArgs e)
        {
            if (MusicListBox.SelectedItem == null) return;

            if (MusicListBox.SelectedItem is TrackItem currentTrack)
            {
                currentTrack.AssignedLabel = string.Empty;
                currentTrack.IsDone = false;
            }
        }

        private void SetupMarqueeAnimation()
        {
            TextTranslate.BeginAnimation(TranslateTransform.XProperty, null);
            TextTranslate.X = 0;

            TxtSeparator.Visibility = Visibility.Collapsed;
            TxtTrackName2.Visibility = Visibility.Collapsed;

            TrackItem selectedTrack = (TrackItem)MusicListBox.SelectedItem;
            TxtTrackName1.Text = selectedTrack.FileName;

            TrackNameCanvas.UpdateLayout();

            double containerWidth = TrackNameCanvas.ActualWidth;
            double singleTextWidth = TxtTrackName1.ActualWidth;

            if (singleTextWidth > containerWidth)
            {
                TxtSeparator.Visibility = Visibility.Visible;
                TxtTrackName2.Visibility = Visibility.Visible;
                TxtTrackName2.Text = selectedTrack.FileName;

                TrackNameCanvas.UpdateLayout();

                double stepWidth = singleTextWidth + TxtSeparator.ActualWidth;

                DoubleAnimation marqueeAnimation = new DoubleAnimation();
                marqueeAnimation.From = 0;
                marqueeAnimation.To = -stepWidth;

                marqueeAnimation.Duration = TimeSpan.FromSeconds(8);
                marqueeAnimation.RepeatBehavior = RepeatBehavior.Forever;

                TextTranslate.BeginAnimation(TranslateTransform.XProperty, marqueeAnimation);
            }
            else
            {
                double panelWidth = TxtTrackNamePanel.ActualWidth;
                Canvas.SetLeft(TxtTrackNamePanel, (containerWidth - panelWidth) / 2);
            }
        }
        private void TrackNameCanvas_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            if (MusicListBox.SelectedItem != null)
            {
                SetupMarqueeAnimation();
            }
        }

        private void MenuItemSettings_Click(object sender, RoutedEventArgs e)
        {
            if (timer.IsEnabled)
            {
                TogglePlayPause();
            }
            WindowSettings windowSettings = new WindowSettings(_appSettings);
            windowSettings.Owner = this;
            if (windowSettings.ShowDialog() == true)
            {
                _appSettings = windowSettings.Settings;
                InitAudioDevice();
                LoadLastSession();
            }
        }
        private void LoadLastSession()
        {
            if (File.Exists(_appSettings.JSONfilePath))
            {
                try
                {
                    string[] lines = File.ReadAllLines(_appSettings.JSONfilePath);
                    var savedLabels = new Dictionary<string, (string Category, bool IsDone)>();
                    foreach (string line in lines)
                    {
                        if (string.IsNullOrWhiteSpace(line)) continue;
                        var annotation = JsonSerializer.Deserialize<AudioAnnotation>(line);
                        if (annotation != null && !string.IsNullOrEmpty(annotation.AudioFile))
                        {
                            savedLabels[annotation.AudioFile] = (annotation.Category, true);
                        }
                    }
                    foreach (var track in _musicList)
                    {
                        if (savedLabels.TryGetValue(track.FileName, out var savedData))
                        {
                            track.AssignedLabel = savedData.Category;
                            track.IsDone = savedData.IsDone;
                        }
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Unable to restore previous markup: {ex.Message}");
                }
            }
        }

        private async void MenuSave_Click(object sender, RoutedEventArgs e)
        {
            if (timer.IsEnabled)
            {
                TogglePlayPause();
            }
            WindowSaving windowSaving = new WindowSaving(_musicList, _appSettings);
            windowSaving.Owner = this;
            windowSaving.BtnOk.Focus();
            windowSaving.ShowDialog();
            if (windowSaving.IsSaveSuccessful)
            {
                _hasUnsavedChanges = false;
            }
        }

        private void TextBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                Window parentWindow = Window.GetWindow(SearchFile_TextBox);
                if (parentWindow != null)
                {
                    FocusManager.SetFocusedElement(parentWindow, parentWindow);
                }
                e.Handled = true;
            }
        }
        private void MusicListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            ButtonFieldInfo.Content= $"{MusicListBox.SelectedIndex}/{_musicList.Count}";
        }

        private void ButtonFieldInfo_Click(object sender, RoutedEventArgs e)
        {
            if (_isShowWaveForm)
            {
                WaveformGrid.Height = 0;
            }
            else
            {
                if (MusicListBox.SelectedItem == null) return;
                WaveformGrid.Height = 100;
                
            }
            _isShowWaveForm = !_isShowWaveForm;
        }

        private async Task RenderWaveformAsync(string filePath)
        {
            _cts?.Cancel();
            _cts = new CancellationTokenSource();
            var token = _cts.Token;

            if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath)) return;

            // Возвращаем фиксированные размеры, которые отлично себя показали
            int width = 1000;
            int height = 100;

            try
            {
                var (topPoints, bottomPoints) = await Task.Run(() =>
                {
                    int[] tops = new int[width];
                    int[] bottoms = new int[width];
                    int midY = height / 2;

                    // --- РЕЖИМ 1: ОДНОПОТОЧНАЯ ОБРАБОТКА (Тихий режим, 0% фризов при спаме) ---
                    if (!_appSettings.MultiThreadedRendering)
                    {
                        using (var reader = new AudioFileReader(filePath))
                        {
                            double totalSeconds = reader.TotalTime.TotalSeconds;
                            long totalSamples = (long)(totalSeconds * reader.WaveFormat.SampleRate * reader.WaveFormat.Channels);
                            int samplesPerPixel = (int)(totalSamples / width);
                            if (samplesPerPixel <= 0) samplesPerPixel = 1;

                            // Выравнивание буфера
                            int alignment = reader.WaveFormat.BlockAlign / (reader.WaveFormat.BitsPerSample / 8);
                            if (alignment <= 0) alignment = 1;
                            samplesPerPixel = ((samplesPerPixel + alignment - 1) / alignment) * alignment;

                            // Выносим создание буфера из цикла (как обсуждали ранее для оптимизации памяти)
                            float[] readBuffer = new float[samplesPerPixel];

                            for (int x = 0; x < width; x++)
                            {
                                if (token.IsCancellationRequested) return (null, null);

                                int samplesRead = reader.Read(readBuffer, 0, samplesPerPixel);
                                if (samplesRead == 0) break;

                                float min = 0;
                                float max = 0;
                                for (int i = 0; i < samplesRead; i++)
                                {
                                    float val = readBuffer[i];
                                    if (val > max) max = val;
                                    if (val < min) min = val;
                                }

                                int topY = midY - (int)(max * midY);
                                int bottomY = midY - (int)(min * midY);

                                tops[x] = Math.Max(0, Math.Min(height - 1, topY));
                                bottoms[x] = Math.Max(0, Math.Min(height - 1, bottomY));
                            }
                        }
                    }
                    // --- РЕЖИМ 2: МНОГОПОТОЧНАЯ ОБРАБОТКА (Турбо-режим для часовых файлов) ---
                    else
                    {
                        int threadCount = Environment.ProcessorCount;
                        int pixelsPerChunk = width / threadCount;

                        // Настройка параметров параллелизма, чтобы не перегружать планировщик Windows
                        var parallelOptions = new ParallelOptions
                        {
                            CancellationToken = token,
                            MaxDegreeOfParallelism = threadCount // Ограничиваем жестко числом ядер
                        };

                        try
                        {
                            Parallel.For(0, threadCount, parallelOptions, i =>
                            {
                                int startX = i * pixelsPerChunk;
                                int endX = (i == threadCount - 1) ? width : startX + pixelsPerChunk;

                                using (var reader = new AudioFileReader(filePath))
                                {
                                    double totalSeconds = reader.TotalTime.TotalSeconds;
                                    long totalSamples = (long)(totalSeconds * reader.WaveFormat.SampleRate * reader.WaveFormat.Channels);
                                    int samplesPerPixel = (int)(totalSamples / width);
                                    if (samplesPerPixel <= 0) samplesPerPixel = 1;

                                    int alignment = reader.WaveFormat.BlockAlign / (reader.WaveFormat.BitsPerSample / 8);
                                    if (alignment <= 0) alignment = 1;
                                    samplesPerPixel = ((samplesPerPixel + alignment - 1) / alignment) * alignment;

                                    long startSample = (long)startX * samplesPerPixel;
                                    long startBytes = startSample * 4;
                                    startBytes = (startBytes / reader.WaveFormat.BlockAlign) * reader.WaveFormat.BlockAlign;

                                    if (startBytes < reader.Length)
                                        reader.Position = startBytes;

                                    float[] readBuffer = new float[samplesPerPixel];

                                    for (int x = startX; x < endX; x++)
                                    {
                                        if (token.IsCancellationRequested) return;

                                        int samplesRead = reader.Read(readBuffer, 0, samplesPerPixel);
                                        if (samplesRead == 0) break;

                                        float min = 0;
                                        float max = 0;
                                        for (int j = 0; j < samplesRead; j++)
                                        {
                                            float val = readBuffer[j];
                                            if (val > max) max = val;
                                            if (val < min) min = val;
                                        }

                                        int topY = midY - (int)(max * midY);
                                        int bottomY = midY - (int)(min * midY);

                                        tops[x] = Math.Max(0, Math.Min(height - 1, topY));
                                        bottoms[x] = Math.Max(0, Math.Min(height - 1, bottomY));
                                    }
                                }
                            });
                        }
                        catch (OperationCanceledException) { return (null, null); }
                    }

                    return (tops, bottoms);
                }, token);

                if (token.IsCancellationRequested || topPoints == null) return;
                var wbitmap = new WriteableBitmap(width, height, 96, 96, PixelFormats.Bgra32, null);
                int stride = wbitmap.BackBufferStride;
                int bytesPerPixel = 4;
                uint waveColor = 0xFF000000;

                wbitmap.Lock();
                unsafe
                {
                    IntPtr pBackBuffer = wbitmap.BackBuffer;
                    for (int x = 0; x < width; x++)
                    {
                        int topY = topPoints[x];
                        int bottomY = bottomPoints[x];

                        for (int y = topY; y <= bottomY; y++)
                        {
                            byte* pPixel = (byte*)pBackBuffer + (y * stride) + (x * bytesPerPixel);
                            *((uint*)pPixel) = waveColor;
                        }
                    }
                }
                wbitmap.AddDirtyRect(new Int32Rect(0, 0, width, height));
                wbitmap.Unlock();

                WaveformImage.Source = wbitmap;
            }
            catch (OperationCanceledException) { }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Ошибка рендеринга: {ex.Message}");
            }
        }

        private void UpdatePlaybackMarker(double currentSeconds, double totalSeconds)
        {
            if (totalSeconds <= 0 || currentSeconds < 0) return;

            if (PlaybackMarker.Visibility != Visibility.Visible)
                PlaybackMarker.Visibility = Visibility.Visible;

            double containerWidth = WaveformImage.ActualWidth;
            if (containerWidth <= 0) return;

            double progress = currentSeconds / totalSeconds;
            if (progress > 1.0) progress = 1.0;

            double leftPosition = progress * containerWidth;

            PlaybackMarker.Margin = new Thickness(leftPosition, 0, 0, 0);
        }

        private void Vizialization_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(_appSettings.JSONfilePath))
            {
                return;
            }
            WindowVizualization windowVizualization = new WindowVizualization(_appSettings.JSONfilePath);
            windowVizualization.Owner = this;
            if (windowVizualization.ShowDialog() == true)
            {
                return;
            }
        }

        private void Reader_Click(object sender, RoutedEventArgs e)
        {
            if (File.Exists(_appSettings.JSONfilePath))
            {
                WindowReader windowReader = new WindowReader(_appSettings.JSONfilePath);
                windowReader.Show();
            } 
            else
            {
                WindowSettings windowSettings = new WindowSettings(_appSettings);
                windowSettings.JSONfilePath_Field.Tag = "Error";
                windowSettings.ErrorMessageLabel.Text = "Select path for JSON file.";
                windowSettings.ErrorMessageLabel.Visibility = Visibility.Visible;
                if (windowSettings.ShowDialog() == true)
                _appSettings = windowSettings.Settings;
            }
        }

        private void MainWindow_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            if (_hasUnsavedChanges)
            {
                var result = MessageBox.Show(
                    "You have unsaved changes. Are you sure you want to exit without saving?",
                    "Warning",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Warning);

                if (result == MessageBoxResult.No)
                {
                    e.Cancel = true;
                }
            }
        }

    }
}