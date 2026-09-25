using LiveChartsCore;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.Painting;
using SkiaSharp;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Windows;

namespace AudioDataLabeling
{
    public partial class WindowVizualization : Window
    {
        private string _JSONfilePath;

        // --- 1. ОБЪЯВЛЕНИЕ КОЛЛЕКЦИЙ ДАННЫХ ---
        private ObservableCollection<double> _columnValues = new ObservableCollection<double>();
        private ObservableCollection<string> _categories = new ObservableCollection<string>();
        private ObservableCollection<double> _radarValues = new ObservableCollection<double>();

        // --- 2. ОБЪЯВЛЕНИЕ СВОЙСТВ ДЛЯ ГРАФИКОВ (СТРОГО НА УРОВНЕ КЛАССА) ---
        public ISeries[] ColumnSeries { get; set; }
        public Axis[] XAxes { get; set; }
        public Axis[] YAxes { get; set; } // Сюда перенесли ось Y

        public ObservableCollection<ISeries> PieSeries { get; set; } = new ObservableCollection<ISeries>();

        public ISeries[] RadarSeries { get; set; }
        public PolarAxis[] RadarAngleAxes { get; set; }
        public PolarAxis[] RadarRadiusAxes { get; set; } // Сюда перенесли радиальную ось

        // --- 3. КОНСТРУКТОР ОКНА ---
        public WindowVizualization(string JSONfilePath)
        {
            InitializeComponent();
            _JSONfilePath = JSONfilePath;

            // Настраиваем сетку и структуру графиков
            InitCharts();

            // Загружаем данные из JSONL файла
            LoadAndGroupData();

            DataContext = this;
        }

        // --- 4. МЕТОД НАСТРОЙКИ СТРУКТУРЫ ГРАФИКОВ ---
        private void InitCharts()
        {
            // Настройка столбчатой диаграммы
            ColumnSeries = new ISeries[]
            {
                new ColumnSeries<double>
                {
                    Values = _columnValues,
                    Name = "Files",
                    Fill = new SolidColorPaint(SKColors.CornflowerBlue),
                    Rx = 6, Ry = 6, MaxBarWidth = 45
                }
            };

            XAxes = new Axis[] { new Axis { Labels = _categories } };

            // Внутри метода мы просто инициализируем свойство, БЕЗ ключевого слова public
            YAxes = new Axis[]
            {
                new Axis
                {
                    SeparatorsPaint = new SolidColorPaint(SKColor.Parse("#CBD5E1")) { StrokeThickness = 1f }
                }
            };

            // Настройка радарной диаграммы
            RadarSeries = new ISeries[]
            {
                new PolarLineSeries<double>
                {
                    Values = _radarValues,
                    Name = "Density",
                    IsClosed = true,
                    Fill = new SolidColorPaint(SKColors.CornflowerBlue.WithAlpha(50)),
                    Stroke = new SolidColorPaint(SKColors.CornflowerBlue) { StrokeThickness = 2 }
                }
            };

            RadarAngleAxes = new PolarAxis[]
            {
                new PolarAxis
                {
                    Labels = _categories,
        
                    LabelsPadding = new LiveChartsCore.Drawing.Padding(15),

                    LabelsPaint = new SolidColorPaint(SKColor.Parse("#475569"))
                }
            };

            RadarRadiusAxes = new PolarAxis[]
            {
                new PolarAxis
                {
                    LabelsAngle = 45,
                    SeparatorsPaint = new SolidColorPaint(SKColors.LightGray.WithAlpha(120)) { StrokeThickness = 1f }
                }
            };
        }

        // --- 5. МЕТОД ПОДСЧЕТА ДАННЫХ ИЗ ФАЙЛА ---
        private void LoadAndGroupData()
        {
            if (!File.Exists(_JSONfilePath)) return;

            try
            {
                string[] lines = File.ReadAllLines(_JSONfilePath);
                var annotations = new List<AudioAnnotation>();

                foreach (string line in lines)
                {
                    if (string.IsNullOrWhiteSpace(line)) continue;
                    var annotation = JsonSerializer.Deserialize<AudioAnnotation>(line);
                    if (annotation != null && !string.IsNullOrEmpty(annotation.Category))
                    {
                        annotations.Add(annotation);
                    }
                }

                var groupedData = annotations
                    .GroupBy(a => a.Category)
                    .Select(g => new { Category = g.Key, Count = (double)g.Count() })
                    .OrderBy(g => g.Category)
                    .ToList();

                _columnValues.Clear();
                _categories.Clear();
                PieSeries.Clear();
                _radarValues.Clear();

                SKColor[] modernColors = {
                    SKColors.CornflowerBlue, SKColors.MediumSeaGreen, SKColors.Orange,
                    SKColors.DeepPink, SKColors.MediumPurple, SKColors.Tomato, SKColors.DarkTurquoise
                };
                int colorIndex = 0;

                foreach (var item in groupedData)
                {
                    _categories.Add(item.Category);
                    _columnValues.Add(item.Count);
                    _radarValues.Add(item.Count);

                    PieSeries.Add(new PieSeries<double>
                    {
                        Values = new double[] { item.Count },
                        Name = item.Category,
                        Fill = new SolidColorPaint(modernColors[colorIndex % modernColors.Length]),
                        OuterRadiusOffset = 5
                    });
                    colorIndex++;
                }

                if (_radarValues.Count > 0)
                {
                    double maxValue = _radarValues.Max();

                    // Делаем запас в 15% от максимального значения (минимум +2 единицы), 
                    // чтобы график никогда не упирался в подписи кнопок
                    double maxWithPadding = maxValue + Math.Max(2, maxValue * 0.3);

                    // Применяем этот максимум к нашей радиальной оси
                    if (RadarRadiusAxes != null && RadarRadiusAxes.Length > 0)
                    {
                        RadarRadiusAxes[0].MaxLimit = maxWithPadding;
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Rendering error: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}
