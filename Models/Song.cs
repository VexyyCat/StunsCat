using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Media.Imaging;

namespace StunsCat.Models
{
    public class Song : INotifyPropertyChanged
    {
        private string _title;
        private string _artist;
        private string _album;
        private string _genre;
        private int _year;
        private TimeSpan _duration;
        private string _filePath;
        private string _fileName;
        private BitmapImage _albumArt;
        private int _bpm;
        private int _bitrate;
        private int _sampleRate;
        private string _format;
        private long _fileSize;
        private DateTime _dateAdded;
        private string _comment;
        private bool _isPlaying;
        private bool _isPaused;

        #region Properties

        public string Title
        {
            get => _title;
            set => SetProperty(ref _title, value);
        }

        public string Artist
        {
            get => _artist;
            set => SetProperty(ref _artist, value);
        }

        public string Album
        {
            get => _album;
            set => SetProperty(ref _album, value);
        }

        public string Genre
        {
            get => _genre;
            set => SetProperty(ref _genre, value);
        }

        public int Year
        {
            get => _year;
            set => SetProperty(ref _year, value);
        }

        public TimeSpan Duration
        {
            get => _duration;
            set => SetProperty(ref _duration, value);
        }

        public string DurationFormatted
        {
            get
            {
                if (Duration.TotalHours >= 1)
                    return Duration.ToString(@"h\:mm\:ss");
                else
                    return Duration.ToString(@"mm\:ss");
            }
        }

        public string FilePath
        {
            get => _filePath;
            set => SetProperty(ref _filePath, value);
        }

        public string FileName
        {
            get => _fileName;
            set => SetProperty(ref _fileName, value);
        }

        public BitmapImage AlbumArt
        {
            get => _albumArt;
            set => SetProperty(ref _albumArt, value);
        }

        public int BPM
        {
            get => _bpm;
            set => SetProperty(ref _bpm, value);
        }

        public int Bitrate
        {
            get => _bitrate;
            set => SetProperty(ref _bitrate, value);
        }

        public int SampleRate
        {
            get => _sampleRate;
            set => SetProperty(ref _sampleRate, value);
        }

        public string Format
        {
            get => _format;
            set => SetProperty(ref _format, value);
        }

        public long FileSize
        {
            get => _fileSize;
            set => SetProperty(ref _fileSize, value);
        }

        public string FileSizeFormatted
        {
            get
            {
                if (FileSize == 0) return "0 B";

                string[] sizes = { "B", "KB", "MB", "GB" };
                double len = FileSize;
                int order = 0;
                while (len >= 1024 && order < sizes.Length - 1)
                {
                    order++;
                    len = len / 1024;
                }
                return $"{len:0.##} {sizes[order]}";
            }
        }

        public DateTime DateAdded
        {
            get => _dateAdded;
            set => SetProperty(ref _dateAdded, value);
        }

        public string Comment
        {
            get => _comment;
            set => SetProperty(ref _comment, value);
        }

        public bool IsPlaying
        {
            get => _isPlaying;
            set => SetProperty(ref _isPlaying, value);
        }

        public bool IsPaused
        {
            get => _isPaused;
            set => SetProperty(ref _isPaused, value);
        }

        // Propiedades adicionales útiles
        public string DisplayName => !string.IsNullOrEmpty(Title) ? Title : FileName;

        public string ArtistAlbum
        {
            get
            {
                if (!string.IsNullOrEmpty(Artist) && !string.IsNullOrEmpty(Album))
                    return $"{Artist} - {Album}";
                if (!string.IsNullOrEmpty(Artist))
                    return Artist;
                if (!string.IsNullOrEmpty(Album))
                    return Album;
                return "Artista desconocido";
            }
        }

        public string YearString => Year > 0 ? Year.ToString() : "Desconocido";

        #endregion

        #region Constructor

        public Song()
        {
            _title = string.Empty;
            _artist = "Artista desconocido";
            _album = "Álbum desconocido";
            _genre = "Sin género";
            _year = 0;
            _duration = TimeSpan.Zero;
            _filePath = string.Empty;
            _fileName = string.Empty;
            _bpm = 120;
            _bitrate = 0;
            _sampleRate = 0;
            _format = string.Empty;
            _fileSize = 0;
            _dateAdded = DateTime.Now;
            _comment = string.Empty;
            _isPlaying = false;
            _isPaused = false;
        }

        public Song(string filePath) : this()
        {
            FilePath = filePath;
            FileName = System.IO.Path.GetFileName(filePath);
            Title = System.IO.Path.GetFileNameWithoutExtension(filePath);
        }

        #endregion

        #region Methods

        public override string ToString()
        {
            return $"{Artist} - {Title}";
        }

        public override bool Equals(object obj)
        {
            if (obj is Song other)
            {
                return string.Equals(FilePath, other.FilePath, StringComparison.OrdinalIgnoreCase);
            }
            return false;
        }

        public override int GetHashCode()
        {
            return FilePath?.GetHashCode() ?? 0;
        }

        public Song Clone()
        {
            return new Song
            {
                Title = this.Title,
                Artist = this.Artist,
                Album = this.Album,
                Genre = this.Genre,
                Year = this.Year,
                Duration = this.Duration,
                FilePath = this.FilePath,
                FileName = this.FileName,
                AlbumArt = this.AlbumArt,
                BPM = this.BPM,
                Bitrate = this.Bitrate,
                SampleRate = this.SampleRate,
                Format = this.Format,
                FileSize = this.FileSize,
                DateAdded = this.DateAdded,
                Comment = this.Comment,
                IsPlaying = this.IsPlaying,
                IsPaused = this.IsPaused
            };
        }

        #endregion

        #region INotifyPropertyChanged Implementation

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

            // Notificar propiedades dependientes
            switch (propertyName)
            {
                case nameof(Duration):
                    OnPropertyChanged(nameof(DurationFormatted));
                    break;
                case nameof(FileSize):
                    OnPropertyChanged(nameof(FileSizeFormatted));
                    break;
                case nameof(Artist):
                case nameof(Album):
                    OnPropertyChanged(nameof(ArtistAlbum));
                    break;
                case nameof(Title):
                case nameof(FileName):
                    OnPropertyChanged(nameof(DisplayName));
                    break;
                case nameof(Year):
                    OnPropertyChanged(nameof(YearString));
                    break;
            }

            return true;
        }

        #endregion
    }
}