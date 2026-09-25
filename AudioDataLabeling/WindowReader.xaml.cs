using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics.Eventing.Reader;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;


namespace AudioDataLabeling
{
    public class WindowReaderViewModel : INotifyPropertyChanged
    {
        private string[] _allLines = Array.Empty<string>();
        private string _searchText = string.Empty;
        private string _matchCountText = "Всего записей: 0"; // Дефолтное значение
        private FlowDocument _document = new FlowDocument();

        public FlowDocument Document
        {
            get => _document;
            set { _document = value; OnPropertyChanged(); }
        }

        public string SearchText
        {
            get => _searchText;
            set
            {
                if (_searchText != value)
                {
                    _searchText = value;
                    OnPropertyChanged();
                    BuildHighlightedDocument();
                }
            }
        }

        // Новое свойство для маленькой подписи счетчика
        public string MatchCountText
        {
            get => _matchCountText;
            set { _matchCountText = value; OnPropertyChanged(); }
        }

        public WindowReaderViewModel(string jsonFilePath)
        {
            LoadFile(jsonFilePath);
        }

        private void LoadFile(string path)
        {
            if (File.Exists(path))
            {
                _allLines = File.ReadAllLines(path);
                BuildHighlightedDocument();
            }
        }

        private void BuildHighlightedDocument()
        {
            var doc = new FlowDocument();
            string query = SearchText.Trim();
            int matchCount = 0; // Переменная для подсчета найденных строк

            foreach (string line in _allLines)
            {
                if (string.IsNullOrWhiteSpace(line)) continue;

                // Если поисковый запрос пустой ИЛИ строка не содержит совпадений
                if (string.IsNullOrEmpty(query) || !line.Contains(query, StringComparison.OrdinalIgnoreCase))
                {
                    if (!string.IsNullOrEmpty(query)) continue; // Пропускаем строку, если ищем конкретное

                    var paragraph = new Paragraph(new Run(line));
                    doc.Blocks.Add(paragraph);
                    continue;
                }

                // Если мы дошли сюда, значит строка содержит совпадение
                matchCount++;

                var lineParagraph = new Paragraph();
                int currentIndex = 0;

                while (currentIndex < line.Length)
                {
                    int matchIndex = line.IndexOf(query, currentIndex, StringComparison.OrdinalIgnoreCase);

                    if (matchIndex == -1)
                    {
                        lineParagraph.Inlines.Add(new Run(line.Substring(currentIndex)));
                        break;
                    }

                    if (matchIndex > currentIndex)
                    {
                        lineParagraph.Inlines.Add(new Run(line.Substring(currentIndex, matchIndex - currentIndex)));
                    }

                    string matchedText = line.Substring(matchIndex, query.Length);
                    var highlightedRun = new Run(matchedText)
                    {
                        Background = Brushes.Yellow,
                        Foreground = Brushes.Black,
                        FontWeight = System.Windows.FontWeights.Bold
                    };
                    lineParagraph.Inlines.Add(highlightedRun);

                    currentIndex = matchIndex + query.Length;
                }

                doc.Blocks.Add(lineParagraph);
            }

            Document = doc;

            // Обновляем текст счетчика в зависимости от того, пустой поиск или нет
            if (string.IsNullOrEmpty(query))
            {
                MatchCountText = $"Total records in the file: {_allLines.Length}";
            }
            else
            {
                MatchCountText = $"Matches found: {matchCount} from {_allLines.Length}";
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string propertyName = "")
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }


    public partial class WindowReader : Window
    {
        private string _JSONfilePath = string.Empty;

        public WindowReader(string jsonFilePath)
        {
            InitializeComponent();

            this.DataContext = new WindowReaderViewModel(jsonFilePath);

        }
    }
}
