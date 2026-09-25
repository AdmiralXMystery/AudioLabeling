using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text;
using System.IO;

namespace AudioDataLabeling
{
    public class TrackItem : INotifyPropertyChanged
    {
        private bool _isDone;
        private string _assignedLabel = string.Empty;

        public string FilePath { get; set; }
        public string FileName => Path.GetFileName(FilePath);
        public bool IsDone
        {
            get => _isDone;
            set
            {
                if (_isDone != value)
                {
                    _isDone = value;
                    OnPropertyChanged();
                }
            }
        }
        public string AssignedLabel
        {
            get => _assignedLabel;
            set { _assignedLabel = value; OnPropertyChanged(); }
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
