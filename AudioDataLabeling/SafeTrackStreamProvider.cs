using NAudio.Wave;
using NAudio.Wave.SampleProviders;
using System;
using System.Collections.Generic;
using System.Text;

namespace AudioDataLabeling
{
    public class SafeTrackStreamProvider : ISampleProvider, IDisposable
    {
        private readonly AudioFileReader _reader;
        private readonly WdlResamplingSampleProvider _resampler;
        private readonly ISampleProvider _outputProvider;
        private readonly object _lock = new object();

        public TimeSpan CurrentTime
        {
            get { lock (_lock) return _reader.CurrentTime; }
            set { lock (_lock) _reader.CurrentTime = value; }
        }

        public TimeSpan TotalTime => _reader.TotalTime;

        public WaveFormat WaveFormat { get; }
        public long Length => _reader.Length;

        public long Position
        {
            get { lock (_lock) return _reader.Position; }
            set { lock (_lock) _reader.Position = value; }
        }

        public SafeTrackStreamProvider(string filePath, WaveFormat targetFormat)
        {
            WaveFormat = targetFormat;
            _reader = new AudioFileReader(filePath);
            _resampler = new WdlResamplingSampleProvider(_reader, targetFormat.SampleRate);

            _outputProvider = _reader.WaveFormat.Channels == 1
                ? _resampler.ToStereo()
                : (ISampleProvider)_resampler;
        }

        public int Read(float[] buffer, int offset, int count)
        {
            lock (_lock)
            {
                return _outputProvider.Read(buffer, offset, count);
            }
        }
        public void Dispose()
        {
            lock (_lock)
            {
                _reader?.Dispose();
            }
        }
    }
}
