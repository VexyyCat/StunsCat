using System;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
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
        private DispatcherTimer _positionTimer;
        private readonly Dispatcher _dispatcher;
        private bool _isDisposed = false;
        private bool _isLoading = false;
        private readonly object _lockObject = new object();

        #region Events
        public event EventHandler<Song> SongChanged;
        public event EventHandler PlaybackStarted;
        public event EventHandler PlaybackPaused;
        public event EventHandler PlaybackStopped;
        public event EventHandler<TimeSpan> PositionChanged;
        public event EventHandler SongEnded;
        #endregion

        #region Properties
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
                    if (_wavePlayer != null && !_isDisposed)
                    {
                        try
                        {
                            _wavePlayer.Volume = _volume;
                        }
                        catch (Exception ex)
                        {
                        }
                    }
                }
            }
        }

        public TimeSpan CurrentPosition
        {
            get => _currentPosition;
            private set => SetProperty(ref _currentPosition, value);
        }

        public TimeSpan TotalDuration => _audioFileReader?.TotalTime ?? TimeSpan.Zero;

        public bool HasSong => CurrentSong != null && !_isDisposed;

        public bool IsLoading => _isLoading;
        #endregion

        #region Constructor
        public AudioPlayerService()
        {
            _dispatcher = Dispatcher.CurrentDispatcher;
            InitializePositionTimer();
        }

        private void InitializePositionTimer()
        {
            _positionTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(100)
            };
            _positionTimer.Tick += OnPositionTimerTick;
        }
        #endregion

        #region Public Methods
        public async Task<bool> LoadSongAsync(Song song)
        {
            if (_isDisposed || _isLoading || song == null)
                return false;

            try
            {
                _isLoading = true;

                // Verificar que el archivo existe
                if (!File.Exists(song.FilePath))
                {
                    return false;
                }

                // Ejecutar en el hilo de UI
                return await _dispatcher.InvokeAsync(() =>
                {
                    lock (_lockObject)
                    {
                        if (_isDisposed || _isLoading == false) // Check if loading was cancelled
                            return false;

                        try
                        {
                            // Detener reproducción actual si existe
                            StopInternal();

                            // Liberar recursos anteriores
                            DisposeCurrentResources();

                            // Crear nuevos recursos de audio
                            _audioFileReader = new AudioFileReader(song.FilePath);

                            // Verificar que el archivo se pudo leer correctamente
                            if (_audioFileReader.TotalTime == TimeSpan.Zero)
                            {
                                _audioFileReader?.Dispose();
                                _audioFileReader = null;
                                return false;
                            }

                            _wavePlayer = new WaveOutEvent();

                            // Configurar el reproductor
                            _wavePlayer.Init(_audioFileReader);
                            _wavePlayer.Volume = _volume;
                            _wavePlayer.PlaybackStopped += OnWavePlayerPlaybackStopped;

                            // Actualizar canción actual
                            CurrentSong = song;

                            // Notificar propiedades actualizadas
                            OnPropertyChanged(nameof(TotalDuration));
                            OnPropertyChanged(nameof(HasSong));

                            // Disparar evento de cambio de canción
                            OnSongChanged(song);
                            return true;
                        }
                        catch (Exception ex)
                        {
                            // Limpiar en caso de error
                            DisposeCurrentResources();
                            CurrentSong = null;
                            OnPropertyChanged(nameof(HasSong));
                            OnPropertyChanged(nameof(TotalDuration));

                            return false;
                        }
                    }
                });
            }
            catch (Exception ex)
            {
                return false;
            }
            finally
            {
                _isLoading = false;
            }
        }

        public void Play()
        {
            if (_isDisposed || _isLoading || _wavePlayer == null || CurrentSong == null)
                return;

            try
            {
                lock (_lockObject)
                {
                    if (_isDisposed || _wavePlayer == null)
                        return;

                    _wavePlayer.Play();
                    IsPlaying = true;
                    IsPaused = false;

                    // Actualizar estado de la canción
                    if (CurrentSong != null)
                    {
                        CurrentSong.IsPlaying = true;
                        CurrentSong.IsPaused = false;
                    }

                    StartPositionTimer();
                    OnPlaybackStarted();
                }
            }
            catch (Exception ex)
            {
                // En caso de error, resetear el estado
                IsPlaying = false;
                IsPaused = false;
                if (CurrentSong != null)
                {
                    CurrentSong.IsPlaying = false;
                    CurrentSong.IsPaused = false;
                }
            }
        }

        public void Pause()
        {
            if (_isDisposed || _wavePlayer == null || !IsPlaying)
                return;

            try
            {
                lock (_lockObject)
                {
                    if (_isDisposed || _wavePlayer == null)
                        return;

                    _wavePlayer.Pause();
                    IsPlaying = false;
                    IsPaused = true;

                    // Actualizar estado de la canción
                    if (CurrentSong != null)
                    {
                        CurrentSong.IsPlaying = false;
                        CurrentSong.IsPaused = true;
                    }

                    StopPositionTimer();
                    OnPlaybackPaused();
                }
            }
            catch (Exception ex)
            {
            }
        }

        public void Stop()
        {
            if (_isDisposed)
                return;

            lock (_lockObject)
            {
                StopInternal();
            }
        }

        public void TogglePlayPause()
        {
            if (_isDisposed || _isLoading)
                return;

            if (IsPlaying)
                Pause();
            else
                Play();
        }

        public void SetPosition(TimeSpan position)
        {
            if (_isDisposed || _audioFileReader == null)
                return;

            try
            {
                lock (_lockObject)
                {
                    if (_isDisposed || _audioFileReader == null)
                        return;

                    var totalSeconds = _audioFileReader.TotalTime.TotalSeconds;
                    var newSeconds = Math.Max(0, Math.Min(totalSeconds, position.TotalSeconds));

                    _audioFileReader.CurrentTime = TimeSpan.FromSeconds(newSeconds);
                    CurrentPosition = TimeSpan.FromSeconds(newSeconds);
                }
            }
            catch (Exception ex)
            {
            }
        }

        public void SetPosition(double percentage)
        {
            if (_isDisposed || _audioFileReader == null)
                return;

            try
            {
                var position = TimeSpan.FromSeconds(_audioFileReader.TotalTime.TotalSeconds * (percentage / 100.0));
                SetPosition(position);
            }
            catch (Exception ex)
            {
            }
        }
        #endregion

        #region Private Methods
        private void StopInternal()
        {
            if (_wavePlayer != null)
            {
                try
                {
                    _wavePlayer.Stop();
                }
                catch (Exception ex)
                {
                }
            }

            IsPlaying = false;
            IsPaused = false;

            // Actualizar estado de la canción
            if (CurrentSong != null)
            {
                CurrentSong.IsPlaying = false;
                CurrentSong.IsPaused = false;
            }

            StopPositionTimer();
            CurrentPosition = TimeSpan.Zero;

            // Reiniciar posición del archivo
            if (_audioFileReader != null)
            {
                try
                {
                    _audioFileReader.Position = 0;
                }
                catch (Exception ex)
                {
                }
            }
            OnPlaybackStopped();
        }

        private void StartPositionTimer()
        {
            if (_isDisposed || _positionTimer == null)
                return;

            try
            {
                if (!_positionTimer.IsEnabled)
                {
                    _positionTimer.Start();
                }
            }
            catch (Exception ex)
            {
            }
        }

        private void StopPositionTimer()
        {
            if (_positionTimer != null && _positionTimer.IsEnabled)
            {
                try
                {
                    _positionTimer.Stop();
                }
                catch (Exception ex)
                {
                }
            }
        }

        private void OnPositionTimerTick(object sender, EventArgs e)
        {
            if (_isDisposed || _audioFileReader == null || !IsPlaying)
                return;

            try
            {
                CurrentPosition = _audioFileReader.CurrentTime;
                OnPositionChanged(CurrentPosition);
            }
            catch (Exception ex)
            {
            }
        }

        private void DisposeCurrentResources()
        {
            try
            {
                StopPositionTimer();

                if (_wavePlayer != null)
                {
                    _wavePlayer.PlaybackStopped -= OnWavePlayerPlaybackStopped;
                    _wavePlayer.Dispose();
                    _wavePlayer = null;
                }

                if (_audioFileReader != null)
                {
                    _audioFileReader.Dispose();
                    _audioFileReader = null;
                }
            }
            catch (Exception ex)
            {
            }
        }
        #endregion

        #region Event Handlers
        private void OnWavePlayerPlaybackStopped(object sender, StoppedEventArgs e)
        {
            if (_isDisposed)
                return;

            try
            {
                // Asegurar que se ejecute en el hilo de UI
                if (!_dispatcher.CheckAccess())
                {
                    _dispatcher.BeginInvoke(() => OnWavePlayerPlaybackStopped(sender, e));
                    return;
                }

                lock (_lockObject)
                {
                    if (_isDisposed)
                        return;

                    IsPlaying = false;
                    IsPaused = false;

                    // Actualizar estado de la canción
                    if (CurrentSong != null)
                    {
                        CurrentSong.IsPlaying = false;
                        CurrentSong.IsPaused = false;
                    }

                    StopPositionTimer();

                    // Verificar si llegó al final de la canción
                    bool reachedEnd = false;
                    if (_audioFileReader != null)
                    {
                        try
                        {
                            var currentPos = _audioFileReader.CurrentTime;
                            var totalTime = _audioFileReader.TotalTime;
                            reachedEnd = currentPos >= totalTime.Subtract(TimeSpan.FromSeconds(1)); // 1 segundo de margen
                        }
                        catch
                        {
                            reachedEnd = false;
                        }
                    }

                    if (reachedEnd)
                    {
                        OnSongEnded();
                    }
                    else
                    {
                        OnPlaybackStopped();
                    }
                }
            }
            catch (Exception ex)
            {
            }
        }
        #endregion

        #region Event Dispatchers
        protected virtual void OnSongChanged(Song song)
        {
            try
            {
                SongChanged?.Invoke(this, song);
            }
            catch (Exception ex)
            {
            }
        }

        protected virtual void OnPlaybackStarted()
        {
            try
            {
                PlaybackStarted?.Invoke(this, EventArgs.Empty);
            }
            catch (Exception ex)
            {
            }
        }

        protected virtual void OnPlaybackPaused()
        {
            try
            {
                PlaybackPaused?.Invoke(this, EventArgs.Empty);
            }
            catch (Exception ex)
            {
            }
        }

        protected virtual void OnPlaybackStopped()
        {
            try
            {
                PlaybackStopped?.Invoke(this, EventArgs.Empty);
            }
            catch (Exception ex)
            {
            }
        }

        protected virtual void OnPositionChanged(TimeSpan position)
        {
            try
            {
                PositionChanged?.Invoke(this, position);
            }
            catch (Exception ex)
            {
            }
        }

        protected virtual void OnSongEnded()
        {
            try
            {
                SongEnded?.Invoke(this, EventArgs.Empty);
            }
            catch (Exception ex)
            {
            }
        }
        #endregion

        #region IDisposable Implementation
        public void Dispose()
        {
            if (_isDisposed)
                return;

            lock (_lockObject)
            {
                if (_isDisposed)
                    return;

                _isDisposed = true;

                try
                {
                    // Detener todo
                    StopInternal();

                    // Liberar recursos de audio
                    DisposeCurrentResources();

                    // Liberar timer
                    if (_positionTimer != null)
                    {
                        _positionTimer.Stop();
                        _positionTimer.Tick -= OnPositionTimerTick;
                        _positionTimer = null;
                    }

                    // Limpiar canción actual
                    CurrentSong = null;
                }
                catch (Exception ex)
                {
                }
            }

            GC.SuppressFinalize(this);
        }
        #endregion

        #region INotifyPropertyChanged Implementation
        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            if (_isDisposed)
                return;

            try
            {
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"❌ Error en PropertyChanged para {propertyName}: {ex.Message}");
            }
        }

        protected bool SetProperty<T>(ref T field, T value, [CallerMemberName] string propertyName = null)
        {
            if (_isDisposed || Equals(field, value))
                return false;

            field = value;
            OnPropertyChanged(propertyName);
            return true;
        }
        #endregion
    }
}