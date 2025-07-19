using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Windows.Threading;
using NAudio.Wave;
using StunsCat.Models;

namespace StunsCat.Services
{
    public class AudioPlayerService : INotifyPropertyChanged, IDisposable
    {
        private IWavePlayer _wavePlayer;
        private AudioFileReader _audioFileReader;
        private Song _currentSong;
        private bool _isPlaying;
        private bool _isPaused;
        private float _volume = 0.5f;
        private TimeSpan _currentPosition;
        private Timer _positionTimer;
        private readonly Dispatcher _dispatcher;

        public event EventHandler<Song> SongChanged;
        public event EventHandler PlaybackStarted;
        public event EventHandler PlaybackPaused;
        public event EventHandler PlaybackStopped;
        public event EventHandler<TimeSpan> PositionChanged;
        public event EventHandler SongEnded;

        public Song CurrentSong
        {
            get => _currentSong;
            private set => SetProperty(ref _currentSong, value);
        }

        public bool IsPlaying
        {
            get => _isPlaying;
            private set => SetProperty(ref _isPlaying, value);
        }

        public bool IsPaused
        {
            get => _isPaused;
            private set => SetProperty(ref _isPaused, value);
        }

        public float Volume
        {
            get => _volume;
            set
            {
                if (SetProperty(ref _volume, Math.Max(0, Math.Min(1, value))))
                {
                    if (_wavePlayer != null)
                        _wavePlayer.Volume = _volume;
                }
            }
        }

        public TimeSpan CurrentPosition
        {
            get => _currentPosition;
            private set => SetProperty(ref _currentPosition, value);
        }

        public TimeSpan TotalDuration => _audioFileReader?.TotalTime ?? TimeSpan.Zero;

        public bool HasSong => CurrentSong != null;

        public AudioPlayerService()
        {
            _dispatcher = Dispatcher.CurrentDispatcher;
        }

        public async Task<bool> LoadSongAsync(Song song)
        {
            return await Task.Run(() =>
            {
                try
                {
                    if (song == null || !File.Exists(song.FilePath))
                        return false;

                    Stop();
                    DisposeCurrentResources();

                    _audioFileReader = new AudioFileReader(song.FilePath);
                    _wavePlayer = new WaveOutEvent();
                    _wavePlayer.Init(_audioFileReader);
                    _wavePlayer.Volume = _volume;
                    _wavePlayer.PlaybackStopped += OnPlaybackStopped;

                    CurrentSong = song;
                    OnSongChanged(song);

                    return true;
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Error loading song: {ex.Message}");
                    return false;
                }
            });
        }

        public void Play()
        {
            if (_wavePlayer != null && CurrentSong != null)
            {
                _wavePlayer.Play();
                IsPlaying = true;
                IsPaused = false;

                if (CurrentSong != null)
                {
                    CurrentSong.IsPlaying = true;
                    CurrentSong.IsPaused = false;
                }

                StartPositionTimer();
                OnPlaybackStarted();
            }
        }

        public void Pause()
        {
            if (_wavePlayer != null && IsPlaying)
            {
                _wavePlayer.Pause();
                IsPlaying = false;
                IsPaused = true;

                if (CurrentSong != null)
                {
                    CurrentSong.IsPlaying = false;
                    CurrentSong.IsPaused = true;
                }

                StopPositionTimer();
                OnPlaybackPaused();
            }
        }

        public void Stop()
        {
            if (_wavePlayer != null)
            {
                _wavePlayer.Stop();
                IsPlaying = false;
                IsPaused = false;

                if (CurrentSong != null)
                {
                    CurrentSong.IsPlaying = false;
                    CurrentSong.IsPaused = false;
                }

                StopPositionTimer();
                CurrentPosition = TimeSpan.Zero;

                if (_audioFileReader != null)
                    _audioFileReader.Position = 0;

                OnPlaybackStopped();
            }
        }

        public void TogglePlayPause()
        {
            if (IsPlaying)
                Pause();
            else
                Play();
        }

        public void SetPosition(TimeSpan position)
        {
            if (_audioFileReader != null)
            {
                var totalSeconds = _audioFileReader.TotalTime.TotalSeconds;
                var newSeconds = Math.Max(0, Math.Min(totalSeconds, position.TotalSeconds));

                _audioFileReader.CurrentTime = TimeSpan.FromSeconds(newSeconds);
                CurrentPosition = TimeSpan.FromSeconds(newSeconds);
            }
        }

        public void SetPosition(double percentage)
        {
            if (_audioFileReader != null)
            {
                var position = TimeSpan.FromSeconds(_audioFileReader.TotalTime.TotalSeconds * percentage);
                SetPosition(position);
            }
        }

        private void StartPositionTimer()
        {
            StopPositionTimer();
            _positionTimer = new Timer(UpdatePosition, null, 0, 100);
        }

        private void StopPositionTimer()
        {
            _positionTimer?.Dispose();
            _positionTimer = null;
        }

        private void UpdatePosition(object state)
        {
            if (_audioFileReader != null && IsPlaying)
            {
                _dispatcher.BeginInvoke(() =>
                {
                    CurrentPosition = _audioFileReader.CurrentTime;
                    OnPositionChanged(CurrentPosition);
                });
            }
        }

        private void OnPlaybackStopped(object sender, StoppedEventArgs e)
        {
            _dispatcher.BeginInvoke(() =>
            {
                IsPlaying = false;
                IsPaused = false;

                if (CurrentSong != null)
                {
                    CurrentSong.IsPlaying = false;
                    CurrentSong.IsPaused = false;
                }

                StopPositionTimer();

                // Si llegó al final de la canción
                if (_audioFileReader != null && _audioFileReader.Position >= _audioFileReader.Length)
                {
                    OnSongEnded();
                }
                else
                {
                    OnPlaybackStopped();
                }
            });
        }

        private void DisposeCurrentResources()
        {
            StopPositionTimer();

            if (_wavePlayer != null)
            {
                _wavePlayer.PlaybackStopped -= OnPlaybackStopped;
                _wavePlayer.Dispose();
                _wavePlayer = null;
            }

            if (_audioFileReader != null)
            {
                _audioFileReader.Dispose();
                _audioFileReader = null;
            }
        }

        public void Dispose()
        {
            Stop();
            DisposeCurrentResources();
        }

        // Eventos
        protected virtual void OnSongChanged(Song song)
        {
            SongChanged?.Invoke(this, song);
        }

        protected virtual void OnPlaybackStarted()
        {
            PlaybackStarted?.Invoke(this, EventArgs.Empty);
        }

        protected virtual void OnPlaybackPaused()
        {
            PlaybackPaused?.Invoke(this, EventArgs.Empty);
        }

        protected virtual void OnPlaybackStopped()
        {
            PlaybackStopped?.Invoke(this, EventArgs.Empty);
        }

        protected virtual void OnPositionChanged(TimeSpan position)
        {
            PositionChanged?.Invoke(this, position);
        }

        protected virtual void OnSongEnded()
        {
            SongEnded?.Invoke(this, EventArgs.Empty);
        }

        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        protected bool SetProperty<T>(ref T field, T value, [CallerMemberName] string propertyName = null)
        {
            if (Equals(field, value)) return false;
            field = value;
            OnPropertyChanged(propertyName);
            return true;
        }
    }
}