using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using Microsoft.Win32;
using StunsCat.Models;
using StunsCat.Services;

namespace StunsCat.ViewModels
{
    public class MainViewModel : INotifyPropertyChanged
    {
        private readonly AudioPlayerService _audioPlayer;
        private readonly MusicScanService _musicScanner;
        private ObservableCollection<Song> _playlist;
        private Song _selectedSong;
        private bool _isScanning;
        private double _scanProgress;
        private string _scanStatus;
        private string _currentBackgroundGif; // Cambié de Video a Gif
        private bool _isVinylRotating;
        private double _vinylRotationAngle;
        private System.Windows.Threading.DispatcherTimer _vinylRotationTimer;

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

        // Cambié el nombre y tipo para GIF
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
                if (_audioPlayer != null)
                {
                    _audioPlayer.Volume = value;
                    OnPropertyChanged();
                }
            }
        }

        public ICommand ScanFolderCommand { get; private set; }
        public ICommand PlayCommand { get; private set; }
        public ICommand PauseCommand { get; private set; }
        public ICommand StopCommand { get; private set; }
        public ICommand TogglePlayPauseCommand { get; private set; }
        public ICommand NextSongCommand { get; private set; }
        public ICommand PreviousSongCommand { get; private set; }
        public ICommand PlaySelectedSongCommand { get; private set; }
        public ICommand SetPositionCommand { get; private set; }

        public MainViewModel()
        {
            _audioPlayer = new AudioPlayerService();
            _musicScanner = new MusicScanService();
            _playlist = new ObservableCollection<Song>();

            InitializeCommands();
            InitializeServices();
            InitializeVinylRotation();
        }

        private void InitializeCommands()
        {
            ScanFolderCommand = new RelayCommand(async () => await ScanFolderAsync());
            PlayCommand = new RelayCommand(() => _audioPlayer.Play(), () => HasSong);
            PauseCommand = new RelayCommand(() => _audioPlayer.Pause(), () => IsPlaying);
            StopCommand = new RelayCommand(() => _audioPlayer.Stop(), () => HasSong);
            TogglePlayPauseCommand = new RelayCommand(() => _audioPlayer.TogglePlayPause(), () => HasSong);
            NextSongCommand = new RelayCommand(() => PlayNextSong());
            PreviousSongCommand = new RelayCommand(() => PlayPreviousSong());
            PlaySelectedSongCommand = new RelayCommand<Song>(async (song) => await PlaySongAsync(song));
            SetPositionCommand = new RelayCommand<double>(percentage => _audioPlayer.SetPosition(percentage));
        }

        private void InitializeServices()
        {
            // Suscribirse a eventos del reproductor
            _audioPlayer.SongChanged += OnSongChanged;
            _audioPlayer.PlaybackStarted += OnPlaybackStarted;
            _audioPlayer.PlaybackPaused += OnPlaybackPaused;
            _audioPlayer.PlaybackStopped += OnPlaybackStopped;
            _audioPlayer.SongEnded += OnSongEnded;
            _audioPlayer.PositionChanged += OnPositionChanged;
            _audioPlayer.PropertyChanged += OnAudioPlayerPropertyChanged;

            // Suscribirse a eventos del escáner
            _musicScanner.ScanProgress += OnScanProgress;
            _musicScanner.ScanStatusChanged += OnScanStatusChanged;
        }

        private void InitializeVinylRotation()
        {
            _vinylRotationTimer = new System.Windows.Threading.DispatcherTimer();
            _vinylRotationTimer.Interval = TimeSpan.FromMilliseconds(50);
            _vinylRotationTimer.Tick += OnVinylRotationTick;
        }

        private void OnVinylRotationTick(object sender, EventArgs e)
        {
            if (IsVinylRotating)
            {
                VinylRotationAngle += 2;
                if (VinylRotationAngle >= 360)
                    VinylRotationAngle = 0;
            }
        }

        private async Task ScanFolderAsync()
        {
            var dialog = new OpenFolderDialog
            {
                Title = "Seleccionar carpeta de música"
            };

            if (dialog.ShowDialog() == true)
            {
                IsScanning = true;
                try
                {
                    var songs = await _musicScanner.ScanDirectoryAsync(dialog.FolderName);
                    Playlist.Clear();
                    foreach (var song in songs)
                    {
                        Playlist.Add(song);
                    }
                }
                catch (Exception ex)
                {
                    ScanStatus = $"Error durante el escaneo: {ex.Message}";
                }
                finally
                {
                    IsScanning = false;
                }
            }
        }

        private async Task PlaySongAsync(Song song)
        {
            if (song != null)
            {
                var success = await _audioPlayer.LoadSongAsync(song);
                if (success)
                {
                    SelectedSong = song;
                    UpdateBackgroundGif(song); // Cambié el método
                    _audioPlayer.Play();
                }
            }
        }

        private void PlayNextSong()
        {
            if (Playlist.Count == 0) return;

            var currentIndex = Playlist.IndexOf(CurrentSong);
            var nextIndex = (currentIndex + 1) % Playlist.Count;
            var nextSong = Playlist[nextIndex];

            _ = PlaySongAsync(nextSong);
        }

        private void PlayPreviousSong()
        {
            if (Playlist.Count == 0) return;

            var currentIndex = Playlist.IndexOf(CurrentSong);
            var prevIndex = currentIndex <= 0 ? Playlist.Count - 1 : currentIndex - 1;
            var prevSong = Playlist[prevIndex];

            _ = PlaySongAsync(prevSong);
        }

        // Método actualizado para GIFs
        private void UpdateBackgroundGif(Song song)
        {
            if (song != null)
            {
                var gifPath = GetBackgroundGifByBPM(song.BPM);

                System.Diagnostics.Debug.WriteLine($"Actualizando GIF de fondo para: {song.Title}");
                System.Diagnostics.Debug.WriteLine($"BPM: {song.BPM}");
                System.Diagnostics.Debug.WriteLine($"GIF path: {gifPath}");

                if (File.Exists(gifPath))
                {
                    CurrentBackgroundGif = gifPath;
                    System.Diagnostics.Debug.WriteLine($"GIF encontrado: {CurrentBackgroundGif}");
                }
                else
                {
                    var defaultPath = Path.GetFullPath("Assets/Gifs/default_bg.gif");
                    System.Diagnostics.Debug.WriteLine($"GIF no encontrado, usando default: {defaultPath}");

                    if (File.Exists(defaultPath))
                    {
                        CurrentBackgroundGif = defaultPath;
                    }
                    else
                    {
                        System.Diagnostics.Debug.WriteLine("GIF por defecto tampoco existe");
                        CurrentBackgroundGif = null;
                    }
                }
            }
        }

        // Método para obtener el GIF basado en BPM
        private string GetBackgroundGifByBPM(int bpm)
        {
            string gifName = bpm switch
            {
                < 80 => "slow_bg.gif",
                >= 80 and < 100 => "medium_bg.gif",
                >= 100 and < 120 => "normal_bg.gif",
                >= 120 and < 140 => "fast_bg.gif",
                _ => "default_bg.gif" // Cambié el valor por defecto
            };

            return Path.GetFullPath($"Assets/Gifs/{gifName}");
        }

        // Event handlers
        private void OnSongChanged(object sender, Song song)
        {
            UpdateBackgroundGif(song);
            OnPropertyChanged(nameof(CurrentSong));
            OnPropertyChanged(nameof(HasSong));
            OnPropertyChanged(nameof(TotalDuration));
        }

        private void OnPlaybackStarted(object sender, EventArgs e)
        {
            IsVinylRotating = true;
            _vinylRotationTimer.Start();
            OnPropertyChanged(nameof(IsPlaying));
            OnPropertyChanged(nameof(IsPaused));
        }

        private void OnPlaybackPaused(object sender, EventArgs e)
        {
            IsVinylRotating = false;
            _vinylRotationTimer.Stop();
            OnPropertyChanged(nameof(IsPlaying));
            OnPropertyChanged(nameof(IsPaused));
        }

        private void OnPlaybackStopped(object sender, EventArgs e)
        {
            IsVinylRotating = false;
            _vinylRotationTimer.Stop();
            VinylRotationAngle = 0;
            OnPropertyChanged(nameof(IsPlaying));
            OnPropertyChanged(nameof(IsPaused));
        }

        private void OnSongEnded(object sender, EventArgs e)
        {
            PlayNextSong();
        }

        private void OnPositionChanged(object sender, TimeSpan position)
        {
            OnPropertyChanged(nameof(CurrentPosition));
        }

        private void OnAudioPlayerPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            OnPropertyChanged(e.PropertyName);
        }

        private void OnScanProgress(object sender, ScanProgressEventArgs e)
        {
            ScanProgress = e.ProgressPercentage;
            ScanStatus = $"Procesando: {e.CurrentFile} ({e.ProcessedFiles}/{e.TotalFiles})";
        }

        private void OnScanStatusChanged(object sender, string status)
        {
            ScanStatus = status;
        }

        public void SeekToPosition(TimeSpan newPosition)
        {
            if (_audioPlayer != null && HasSong)
            {
                var percentage = TotalDuration.TotalSeconds > 0
                    ? (newPosition.TotalSeconds / TotalDuration.TotalSeconds) * 100
                    : 0;

                _audioPlayer.SetPosition(percentage);
            }
        }

        public void Dispose()
        {
            _vinylRotationTimer?.Stop();
            _audioPlayer?.Dispose();
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

    // RelayCommand classes remain the same
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
            return _canExecute?.Invoke() ?? true;
        }

        public void Execute(object parameter)
        {
            _execute();
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
            return _canExecute?.Invoke((T)parameter) ?? true;
        }

        public void Execute(object parameter)
        {
            _execute((T)parameter);
        }
    }
}