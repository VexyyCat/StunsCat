using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows.Input;
using System.Windows.Threading;
using Microsoft.Win32;
using StunsCat.Models;
using StunsCat.Services;

namespace StunsCat.ViewModels
{
    public class MainViewModel : INotifyPropertyChanged, IDisposable
    {
        private readonly AudioPlayerService _audioPlayer;
        private readonly MusicScanService _musicScanner;
        private readonly PlaylistManager _playlistManager;
        private readonly Dispatcher _dispatcher;
        private ObservableCollection<Song> _playlist;
        private Song _selectedSong;
        private bool _isScanning;
        private double _scanProgress;
        private string _scanStatus;
        private string _currentBackgroundGif;
        private bool _isVinylRotating;
        private double _vinylRotationAngle;
        private DispatcherTimer _vinylRotationTimer;
        private DispatcherTimer _positionTimer;
        private bool _disposed = false;
        private bool _isSongLoading = false;

        #region Properties

        public ObservableCollection<Song> Playlist
        {
            get => _playlist;
            set => SetProperty(ref _playlist, value);
        }

        public Song SelectedSong
        {
            get => _selectedSong;
            set => SetProperty(ref _selectedSong, value);
        }

        public bool IsScanning
        {
            get => _isScanning;
            set => SetProperty(ref _isScanning, value);
        }

        public double ScanProgress
        {
            get => _scanProgress;
            set => SetProperty(ref _scanProgress, value);
        }

        public string ScanStatus
        {
            get => _scanStatus;
            set => SetProperty(ref _scanStatus, value);
        }

        public string CurrentBackgroundGif
        {
            get => _currentBackgroundGif;
            set => SetProperty(ref _currentBackgroundGif, value);
        }

        public bool IsVinylRotating
        {
            get => _isVinylRotating;
            set => SetProperty(ref _isVinylRotating, value);
        }

        public double VinylRotationAngle
        {
            get => _vinylRotationAngle;
            set => SetProperty(ref _vinylRotationAngle, value);
        }

        public bool IsSongLoading
        {
            get => _isSongLoading;
            set => SetProperty(ref _isSongLoading, value);
        }

        // Propiedades del reproductor
        public Song CurrentSong => _audioPlayer?.CurrentSong;
        public bool IsPlaying => _audioPlayer?.IsPlaying ?? false;
        public bool IsPaused => _audioPlayer?.IsPaused ?? false;
        public bool HasSong => _audioPlayer?.HasSong ?? false;
        public TimeSpan CurrentPosition => _audioPlayer?.CurrentPosition ?? TimeSpan.Zero;
        public TimeSpan TotalDuration => _audioPlayer?.TotalDuration ?? TimeSpan.Zero;

        public float Volume
        {
            get => _audioPlayer?.Volume ?? 0f;
            set
            {
                if (_audioPlayer != null && !_disposed)
                {
                    _audioPlayer.Volume = value;
                    OnPropertyChanged();
                }
            }
        }

        public bool IsShuffleEnabled { get; private set; }
        public bool IsLoopEnabled { get; private set; }

        // PlaylistManager para el XAML
        public PlaylistManager PlaylistManager => _playlistManager;

        #endregion

        #region Commands

        public ICommand ScanFolderCommand { get; private set; }
        public ICommand PlayCommand { get; private set; }
        public ICommand PauseCommand { get; private set; }
        public ICommand StopCommand { get; private set; }
        public ICommand TogglePlayPauseCommand { get; private set; }
        public ICommand NextSongCommand { get; private set; }
        public ICommand PreviousSongCommand { get; private set; }
        public ICommand PlaySelectedSongCommand { get; private set; }
        public ICommand SetPositionCommand { get; private set; }
        public ICommand ToggleShuffleCommand { get; private set; }
        public ICommand ToggleLoopCommand { get; private set; }
        public ICommand LoadPlaylistCommand { get; private set; }

        #endregion

        #region Constructor

        public MainViewModel()
        {
            try
            {
                _dispatcher = Dispatcher.CurrentDispatcher;
                _audioPlayer = new AudioPlayerService();
                _musicScanner = new MusicScanService();
                _playlistManager = new PlaylistManager();
                _playlist = new ObservableCollection<Song>();

                InitializeCommands();
                InitializeServices();
                InitializeTimers();
                LoadConfiguration();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"❌ Error inicializando MainViewModel: {ex.Message}");
                throw;
            }
        }

        #endregion

        #region Initialization

        private void InitializeCommands()
        {
            ScanFolderCommand = new RelayCommand(async () => await ScanFolderAsync(), () => !IsScanning);
            PlayCommand = new RelayCommand(() => SafeExecute(() => _audioPlayer?.Play()), () => HasSong && !IsSongLoading);
            PauseCommand = new RelayCommand(() => SafeExecute(() => _audioPlayer?.Pause()), () => IsPlaying && !IsSongLoading);
            StopCommand = new RelayCommand(() => SafeExecute(() => _audioPlayer?.Stop()), () => HasSong && !IsSongLoading);
            TogglePlayPauseCommand = new RelayCommand(() => SafeExecute(() => _audioPlayer?.TogglePlayPause()), () => HasSong && !IsSongLoading);
            NextSongCommand = new RelayCommand(() => PlayNextSong(), () => Playlist?.Count > 1 && !IsSongLoading);
            PreviousSongCommand = new RelayCommand(() => PlayPreviousSong(), () => Playlist?.Count > 1 && !IsSongLoading);
            PlaySelectedSongCommand = new RelayCommand<Song>(async (song) => await PlaySongAsync(song), (song) => song != null && !IsSongLoading);
            SetPositionCommand = new RelayCommand<double>(percentage => SafeExecute(() => _audioPlayer?.SetPosition(percentage)), (percentage) => HasSong && !IsSongLoading);
            ToggleShuffleCommand = new RelayCommand(() => SafeExecute(() => ToggleShuffle()), () => !IsSongLoading);
            ToggleLoopCommand = new RelayCommand(() => SafeExecute(() => ToggleLoop()), () => !IsSongLoading);
            LoadPlaylistCommand = new RelayCommand<Playlist>(LoadPlaylist, (playlist) => playlist != null);
        }

        private void InitializeServices()
        {
            if (_audioPlayer != null)
            {
                // Suscribirse a eventos del reproductor
                _audioPlayer.SongChanged += OnSongChanged;
                _audioPlayer.PlaybackStarted += OnPlaybackStarted;
                _audioPlayer.PlaybackPaused += OnPlaybackPaused;
                _audioPlayer.PlaybackStopped += OnPlaybackStopped;
                _audioPlayer.SongEnded += OnSongEnded;
                _audioPlayer.PositionChanged += OnPositionChanged;

                if (_audioPlayer is INotifyPropertyChanged notifyPropertyChanged)
                {
                    notifyPropertyChanged.PropertyChanged += OnAudioPlayerPropertyChanged;
                }
            }

            if (_musicScanner != null)
            {
                // Suscribirse a eventos del escáner
                _musicScanner.ScanProgress += OnScanProgress;
                _musicScanner.ScanStatusChanged += OnScanStatusChanged;
            }
        }

        private void InitializeTimers()
        {
            // Timer para rotación del vinilo
            _vinylRotationTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(50)
            };
            _vinylRotationTimer.Tick += OnVinylRotationTick;

            // Timer para actualizar posición
            _positionTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(100)
            };
            _positionTimer.Tick += OnPositionTimerTick;
        }

        #endregion

        #region Utility Methods

        private void SafeExecute(Action action)
        {
            if (_disposed || action == null) return;

            try
            {
                if (_dispatcher.CheckAccess())
                {
                    action.Invoke();
                }
                else
                {
                    _dispatcher.BeginInvoke(action);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"❌ Error ejecutando acción: {ex.Message}");
            }
        }

        private void ToggleShuffle()
        {
            IsShuffleEnabled = !IsShuffleEnabled;
            OnPropertyChanged(nameof(IsShuffleEnabled));
            System.Diagnostics.Debug.WriteLine($"🔀 Shuffle: {(IsShuffleEnabled ? "Activado" : "Desactivado")}");
        }

        private void ToggleLoop()
        {
            IsLoopEnabled = !IsLoopEnabled;
            OnPropertyChanged(nameof(IsLoopEnabled));
            System.Diagnostics.Debug.WriteLine($"🔁 Loop: {(IsLoopEnabled ? "Activado" : "Desactivado")}");
        }

        private void RefreshPlayerProperties()
        {
            if (_disposed) return;

            SafeExecute(() =>
            {
                OnPropertyChanged(nameof(CurrentSong));
                OnPropertyChanged(nameof(IsPlaying));
                OnPropertyChanged(nameof(IsPaused));
                OnPropertyChanged(nameof(HasSong));
                OnPropertyChanged(nameof(CurrentPosition));
                OnPropertyChanged(nameof(TotalDuration));
                OnPropertyChanged(nameof(Volume));
            });
        }

        public void SeekToPosition(TimeSpan newPosition)
        {
            if (_disposed || _audioPlayer == null || !HasSong || IsSongLoading) return;

            try
            {
                _audioPlayer.SetPosition(newPosition);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"❌ Error buscando posición: {ex.Message}");
            }
        }

        private void LoadPlaylist(Playlist playlist)
        {
            if (playlist == null || _disposed) return;

            try
            {
                Playlist.Clear();
                foreach (var song in playlist.Songs)
                {
                    Playlist.Add(song);
                }

                System.Diagnostics.Debug.WriteLine($"✅ Cargada playlist: {playlist.Name} con {playlist.Songs.Count} canciones");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"❌ Error cargando playlist: {ex.Message}");
            }
        }

        #endregion

        #region Animation Timers

        private void OnVinylRotationTick(object sender, EventArgs e)
        {
            if (IsVinylRotating && !_disposed)
            {
                VinylRotationAngle += 2;
                if (VinylRotationAngle >= 360)
                    VinylRotationAngle = 0;
            }
        }

        private void OnPositionTimerTick(object sender, EventArgs e)
        {
            if (!_disposed && HasSong && IsPlaying)
            {
                OnPropertyChanged(nameof(CurrentPosition));
            }
        }

        #endregion

        #region Music Operations

        private async Task ScanFolderAsync()
        {
            if (_disposed || IsScanning) return;

            try
            {
                var dialog = new OpenFolderDialog
                {
                    Title = "Seleccionar carpeta de música"
                };

                if (dialog.ShowDialog() == true)
                {
                    IsScanning = true;
                    ScanStatus = "Iniciando escaneo...";

                    var songs = await Task.Run(() => _musicScanner.ScanDirectoryAsync(dialog.FolderName));

                    if (!_disposed)
                    {
                        await _dispatcher.BeginInvoke(() =>
                        {
                            Playlist.Clear();
                            foreach (var song in songs)
                            {
                                Playlist.Add(song);
                            }

                            // Crear playlists automáticamente
                            _playlistManager.CreatePlaylistsFromSongs(songs);

                            ScanStatus = $"Escaneo completado. {songs.Count} canciones encontradas.";
                            System.Diagnostics.Debug.WriteLine($"✅ Escaneo completado: {songs.Count} canciones, {_playlistManager.Playlists.Count} playlists");
                        });
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"❌ Error durante el escaneo: {ex.Message}");
                ScanStatus = $"Error durante el escaneo: {ex.Message}";
            }
            finally
            {
                IsScanning = false;
            }
        }

        private async Task PlaySongAsync(Song song)
        {
            if (song == null || _disposed || IsSongLoading) return;

            try
            {
                IsSongLoading = true;
                System.Diagnostics.Debug.WriteLine($"🎵 Cargando canción: {song.Title}");

                // Parar reproducción actual si existe
                if (IsPlaying)
                {
                    _audioPlayer?.Stop();
                }

                // Cargar nueva canción
                bool success = await _audioPlayer.LoadSongAsync(song);

                if (success && !_disposed)
                {
                    SelectedSong = song;
                    UpdateBackgroundGif(song);
                    RefreshPlayerProperties();

                    // Iniciar reproducción
                    _audioPlayer?.Play();

                    System.Diagnostics.Debug.WriteLine($"✅ Canción cargada exitosamente: {song.Title}");
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine($"❌ Error cargando canción: {song.Title}");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"❌ Error reproduciendo canción: {ex.Message}");
            }
            finally
            {
                IsSongLoading = false;
            }
        }

        private void PlayNextSong()
        {
            if (Playlist.Count == 0 || _disposed || IsSongLoading) return;

            try
            {
                var currentIndex = CurrentSong != null ? Playlist.IndexOf(CurrentSong) : -1;
                var nextIndex = IsShuffleEnabled ?
                    new Random().Next(0, Playlist.Count) :
                    (currentIndex + 1) % Playlist.Count;
                var nextSong = Playlist[nextIndex];

                _ = PlaySongAsync(nextSong);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"❌ Error reproduciendo siguiente canción: {ex.Message}");
            }
        }

        private void PlayPreviousSong()
        {
            if (Playlist.Count == 0 || _disposed || IsSongLoading) return;

            try
            {
                var currentIndex = CurrentSong != null ? Playlist.IndexOf(CurrentSong) : 0;
                var prevIndex = IsShuffleEnabled ?
                    new Random().Next(0, Playlist.Count) :
                    (currentIndex <= 0 ? Playlist.Count - 1 : currentIndex - 1);
                var prevSong = Playlist[prevIndex];

                _ = PlaySongAsync(prevSong);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"❌ Error reproduciendo canción anterior: {ex.Message}");
            }
        }

        #endregion

        #region Background GIF Management

        private void UpdateBackgroundGif(Song song)
        {
            if (song == null || _disposed) return;

            try
            {
                var gifPath = GetBackgroundGifByBPM(song.BPM);

                System.Diagnostics.Debug.WriteLine($"🎭 Actualizando GIF de fondo para: {song.Title}");
                System.Diagnostics.Debug.WriteLine($"🎵 BPM: {song.BPM}");
                System.Diagnostics.Debug.WriteLine($"📁 GIF path: {gifPath}");

                if (File.Exists(gifPath))
                {
                    CurrentBackgroundGif = gifPath;
                    System.Diagnostics.Debug.WriteLine($"✅ GIF encontrado: {CurrentBackgroundGif}");
                }
                else
                {
                    var defaultPath = Path.GetFullPath("Assets/Gifs/default_bg.gif");
                    System.Diagnostics.Debug.WriteLine($"⚠️ GIF no encontrado, usando default: {defaultPath}");

                    CurrentBackgroundGif = File.Exists(defaultPath) ? defaultPath : null;

                    if (CurrentBackgroundGif == null)
                    {
                        System.Diagnostics.Debug.WriteLine("❌ GIF por defecto tampoco existe");
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"❌ Error actualizando GIF de fondo: {ex.Message}");
                CurrentBackgroundGif = null;
            }
        }

        private string GetBackgroundGifByBPM(int bpm)
        {
            string gifName = bpm switch
            {
                < 80 => "slow_bg.gif",
                >= 80 and < 100 => "medium_bg.gif",
                >= 100 and < 120 => "normal_bg.gif",
                >= 120 and < 140 => "fast_bg.gif",
                _ => "default_bg.gif"
            };

            return Path.GetFullPath($"Assets/Gifs/{gifName}");
        }

        #endregion

        #region Event Handlers

        private void OnSongChanged(object sender, Song song)
        {
            if (_disposed) return;

            SafeExecute(() =>
            {
                UpdateBackgroundGif(song);
                RefreshPlayerProperties();
                System.Diagnostics.Debug.WriteLine($"🎵 Canción cambiada: {song?.Title ?? "Ninguna"}");
            });
        }

        private void OnPlaybackStarted(object sender, EventArgs e)
        {
            if (_disposed) return;

            SafeExecute(() =>
            {
                IsVinylRotating = true;
                _vinylRotationTimer?.Start();
                _positionTimer?.Start();
                RefreshPlayerProperties();
                System.Diagnostics.Debug.WriteLine("▶️ Reproducción iniciada");
            });
        }

        private void OnPlaybackPaused(object sender, EventArgs e)
        {
            if (_disposed) return;

            SafeExecute(() =>
            {
                IsVinylRotating = false;
                _vinylRotationTimer?.Stop();
                _positionTimer?.Stop();
                RefreshPlayerProperties();
                System.Diagnostics.Debug.WriteLine("⏸️ Reproducción pausada");
            });
        }

        private void OnPlaybackStopped(object sender, EventArgs e)
        {
            if (_disposed) return;

            SafeExecute(() =>
            {
                IsVinylRotating = false;
                _vinylRotationTimer?.Stop();
                _positionTimer?.Stop();
                VinylRotationAngle = 0;
                RefreshPlayerProperties();
                System.Diagnostics.Debug.WriteLine("⏹️ Reproducción detenida");
            });
        }

        private void OnSongEnded(object sender, EventArgs e)
        {
            if (_disposed) return;

            SafeExecute(() =>
            {
                if (IsLoopEnabled)
                {
                    // Repetir la canción actual
                    _audioPlayer?.SetPosition(0);
                    _audioPlayer?.Play();
                }
                else
                {
                    PlayNextSong();
                }
            });
        }

        private void OnPositionChanged(object sender, TimeSpan position)
        {
            if (_disposed) return;
            // No hacer nada aquí, usamos el timer para mejor performance
        }

        private void OnAudioPlayerPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (_disposed) return;
            SafeExecute(() => OnPropertyChanged(e.PropertyName));
        }

        private void OnScanProgress(object sender, ScanProgressEventArgs e)
        {
            if (_disposed) return;

            SafeExecute(() =>
            {
                ScanProgress = e.ProgressPercentage;
                ScanStatus = $"Procesando: {e.CurrentFile} ({e.ProcessedFiles}/{e.TotalFiles})";
            });
        }

        private void OnScanStatusChanged(object sender, string status)
        {
            if (_disposed) return;
            SafeExecute(() => ScanStatus = status);
        }

        #endregion

        #region Configuration

        internal void LoadConfiguration()
        {
            try
            {
                string configPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "config.json");
                if (!File.Exists(configPath))
                {
                    System.Diagnostics.Debug.WriteLine("⚠️ Archivo de configuración no encontrado.");
                    return;
                }

                string json = File.ReadAllText(configPath);
                var config = System.Text.Json.JsonSerializer.Deserialize<MainViewModelConfig>(json);

                if (config != null)
                {
                    // Asignar propiedades configurables
                    if (_audioPlayer != null)
                    {
                        _audioPlayer.Volume = config.Volume;
                    }
                    IsShuffleEnabled = config.IsShuffleEnabled;
                    IsLoopEnabled = config.IsLoopEnabled;
                    OnPropertyChanged(nameof(IsShuffleEnabled));
                    OnPropertyChanged(nameof(IsLoopEnabled));
                    OnPropertyChanged(nameof(Volume));
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"❌ Error cargando configuración: {ex.Message}");
            }
        }

        internal void SaveConfiguration()
        {
            try
            {
                string configPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "config.json");
                var config = new MainViewModelConfig
                {
                    Volume = _audioPlayer?.Volume ?? 0.5f,
                    IsShuffleEnabled = IsShuffleEnabled,
                    IsLoopEnabled = IsLoopEnabled
                };

                var options = new System.Text.Json.JsonSerializerOptions
                {
                    WriteIndented = true
                };

                string json = System.Text.Json.JsonSerializer.Serialize(config, options);
                File.WriteAllText(configPath, json);
                System.Diagnostics.Debug.WriteLine("💾 Configuración guardada correctamente.");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"❌ Error guardando configuración: {ex.Message}");
            }
        }

        internal void ApplyTheme(string themeName)
        {
            if (string.IsNullOrWhiteSpace(themeName))
                return;

            try
            {
                // Buscar el diccionario de recursos del tema en la carpeta Themes
                string themePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Themes", $"{themeName}.xaml");
                if (!File.Exists(themePath))
                {
                    System.Diagnostics.Debug.WriteLine($"⚠️ Tema no encontrado: {themePath}");
                    return;
                }

                // Cargar el ResourceDictionary del tema
                var themeDict = new System.Windows.ResourceDictionary
                {
                    Source = new Uri(themePath, UriKind.Absolute)
                };

                // Obtener la aplicación actual
                var app = System.Windows.Application.Current;
                if (app == null)
                    return;

                // Buscar y eliminar diccionarios de temas previos
                for (int i = app.Resources.MergedDictionaries.Count - 1; i >= 0; i--)
                {
                    var dict = app.Resources.MergedDictionaries[i];
                    if (dict.Source != null && dict.Source.OriginalString.Contains(@"\Themes\"))
                    {
                        app.Resources.MergedDictionaries.RemoveAt(i);
                    }
                }

                // Agregar el nuevo tema
                app.Resources.MergedDictionaries.Add(themeDict);
                System.Diagnostics.Debug.WriteLine($"🎨 Tema aplicado: {themeName}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"❌ Error aplicando tema: {ex.Message}");
            }
        }

        // Clase auxiliar para deserializar la configuración
        private class MainViewModelConfig
        {
            public float Volume { get; set; } = 0.5f;
            public bool IsShuffleEnabled { get; set; } = false;
            public bool IsLoopEnabled { get; set; } = false;
        }

        #endregion

        #region IDisposable Implementation

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing)
                {
                    try
                    {
                        // Guardar configuración antes de cerrar
                        SaveConfiguration();

                        // Detener todos los timers
                        if (_vinylRotationTimer != null)
                        {
                            _vinylRotationTimer.Stop();
                            _vinylRotationTimer.Tick -= OnVinylRotationTick;
                            _vinylRotationTimer = null;
                        }

                        if (_positionTimer != null)
                        {
                            _positionTimer.Stop();
                            _positionTimer.Tick -= OnPositionTimerTick;
                            _positionTimer = null;
                        }

                        // Desuscribirse de eventos del reproductor
                        if (_audioPlayer != null)
                        {
                            _audioPlayer.SongChanged -= OnSongChanged;
                            _audioPlayer.PlaybackStarted -= OnPlaybackStarted;
                            _audioPlayer.PlaybackPaused -= OnPlaybackPaused;
                            _audioPlayer.PlaybackStopped -= OnPlaybackStopped;
                            _audioPlayer.SongEnded -= OnSongEnded;
                            _audioPlayer.PositionChanged -= OnPositionChanged;

                            if (_audioPlayer is INotifyPropertyChanged notifyPropertyChanged)
                            {
                                notifyPropertyChanged.PropertyChanged -= OnAudioPlayerPropertyChanged;
                            }

                            _audioPlayer.Dispose();
                        }

                        // Desuscribirse de eventos del escáner
                        if (_musicScanner != null)
                        {
                            _musicScanner.ScanProgress -= OnScanProgress;
                            _musicScanner.ScanStatusChanged -= OnScanStatusChanged;
                            _musicScanner.Dispose();
                        }

                        // Limpiar colección
                        _playlist?.Clear();
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"❌ Error durante Dispose: {ex.Message}");
                    }
                }

                _disposed = true;
            }
        }

        #endregion

        #region INotifyPropertyChanged Implementation

        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            if (_disposed) return;

            SafeExecute(() =>
            {
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
            });
        }

        protected bool SetProperty<T>(ref T field, T value, [CallerMemberName] string propertyName = null)
        {
            if (_disposed || Equals(field, value)) return false;

            field = value;
            OnPropertyChanged(propertyName);
            return true;
        }

        #endregion
    }

    #region Command Classes

    public class RelayCommand : ICommand
    {
        private readonly Action _execute;
        private readonly Func<bool> _canExecute;

        public RelayCommand(Action execute, Func<bool> canExecute = null)
        {
            _execute = execute ?? throw new ArgumentNullException(nameof(execute));
            _canExecute = canExecute;
        }

        public event EventHandler CanExecuteChanged
        {
            add { CommandManager.RequerySuggested += value; }
            remove { CommandManager.RequerySuggested -= value; }
        }

        public bool CanExecute(object parameter)
        {
            try
            {
                return _canExecute?.Invoke() ?? true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"❌ Error en CanExecute: {ex.Message}");
                return false;
            }
        }

        public void Execute(object parameter)
        {
            try
            {
                if (CanExecute(parameter))
                {
                    _execute();
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"❌ Error ejecutando comando: {ex.Message}");
            }
        }
    }

    public class RelayCommand<T> : ICommand
    {
        private readonly Action<T> _execute;
        private readonly Func<T, bool> _canExecute;

        public RelayCommand(Action<T> execute, Func<T, bool> canExecute = null)
        {
            _execute = execute ?? throw new ArgumentNullException(nameof(execute));
            _canExecute = canExecute;
        }

        public event EventHandler CanExecuteChanged
        {
            add { CommandManager.RequerySuggested += value; }
            remove { CommandManager.RequerySuggested -= value; }
        }

        public bool CanExecute(object parameter)
        {
            try
            {
                return _canExecute?.Invoke((T)parameter) ?? true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"❌ Error en CanExecute<T>: {ex.Message}");
                return false;
            }
        }

        public void Execute(object parameter)
        {
            try
            {
                if (CanExecute(parameter))
                {
                    _execute((T)parameter);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"❌ Error ejecutando comando<T>: {ex.Message}");
            }
        }
    }

    #endregion
}