using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace StunsCat.Models
{
    /// <summary>
    /// Modelo para representar una lista de reproducción
    /// </summary>
    public class Playlist : INotifyPropertyChanged
    {
        private string _name;
        private string _genre;
        private int _songCount;
        private string _totalDuration;
        private ObservableCollection<Song> _songs;
        private bool _isSelected;

        public string Name
        {
            get => _name;
            set
            {
                _name = value;
                OnPropertyChanged();
            }
        }

        public string Genre
        {
            get => _genre;
            set
            {
                _genre = value;
                OnPropertyChanged();
            }
        }

        public int SongCount
        {
            get => _songCount;
            set
            {
                _songCount = value;
                OnPropertyChanged();
            }
        }

        public string TotalDuration
        {
            get => _totalDuration;
            set
            {
                _totalDuration = value;
                OnPropertyChanged();
            }
        }

        public ObservableCollection<Song> Songs
        {
            get => _songs;
            set
            {
                _songs = value;
                OnPropertyChanged();
                UpdatePlaylistInfo();
            }
        }

        public bool IsSelected
        {
            get => _isSelected;
            set
            {
                _isSelected = value;
                OnPropertyChanged();
            }
        }

        public string PlaylistIcon
        {
            get
            {
                return Genre?.ToLower() switch
                {
                    "rock" => "🎸",
                    "pop" => "🎤",
                    "jazz" => "🎷",
                    "classical" => "🎼",
                    "electronic" => "🎧",
                    "hip hop" => "🎤",
                    "country" => "🤠",
                    "reggae" => "🌴",
                    "metal" => "🤘",
                    "blues" => "🎺",
                    "folk" => "🪕",
                    "r&b" => "🎵",
                    "latin" => "💃",
                    "ambient" => "🌙",
                    "dance" => "💃",
                    _ => "🎵"
                };
            }
        }

        public Playlist()
        {
            Songs = new ObservableCollection<Song>();
            Songs.CollectionChanged += Songs_CollectionChanged;
        }

        public Playlist(string genre, IEnumerable<Song> songs) : this()
        {
            Genre = genre;
            Name = string.IsNullOrEmpty(genre) ? "Sin Género" : genre;

            foreach (var song in songs)
            {
                Songs.Add(song);
            }

            UpdatePlaylistInfo();
        }

        private void Songs_CollectionChanged(object sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        {
            UpdatePlaylistInfo();
        }

        private void UpdatePlaylistInfo()
        {
            if (Songs == null) return;

            SongCount = Songs.Count;

            // Calcular duración total
            TimeSpan totalTime = TimeSpan.Zero;
            foreach (var song in Songs)
            {
                totalTime = totalTime.Add(song.Duration);
            }

            TotalDuration = FormatDuration(totalTime);
        }

        private string FormatDuration(TimeSpan duration)
        {
            if (duration.TotalHours >= 1)
                return $"{(int)duration.TotalHours:D2}:{duration.Minutes:D2}:{duration.Seconds:D2}";
            else
                return $"{duration.Minutes:D2}:{duration.Seconds:D2}";
        }

        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    /// <summary>
    /// Gestor de listas de reproducción automáticas por géneros
    /// </summary>
    public class PlaylistManager : INotifyPropertyChanged
    {
        private ObservableCollection<Playlist> _playlists;
        private Playlist _selectedPlaylist;
        private ObservableCollection<Song> _allSongs;

        public ObservableCollection<Playlist> Playlists
        {
            get => _playlists;
            set
            {
                _playlists = value;
                OnPropertyChanged();
            }
        }

        public Playlist SelectedPlaylist
        {
            get => _selectedPlaylist;
            set
            {
                if (_selectedPlaylist != null)
                    _selectedPlaylist.IsSelected = false;

                _selectedPlaylist = value;

                if (_selectedPlaylist != null)
                    _selectedPlaylist.IsSelected = true;

                OnPropertyChanged();
                OnPropertyChanged(nameof(SelectedPlaylistSongs));
            }
        }

        public ObservableCollection<Song> SelectedPlaylistSongs
        {
            get => _selectedPlaylist?.Songs ?? new ObservableCollection<Song>();
        }

        public PlaylistManager()
        {
            Playlists = new ObservableCollection<Playlist>();
            _allSongs = new ObservableCollection<Song>();
        }

        /// <summary>
        /// Genera listas de reproducción automáticamente basadas en los géneros de las canciones
        /// </summary>
        /// <param name="songs">Colección de canciones</param>
        public void GeneratePlaylistsFromSongs(IEnumerable<Song> songs)
        {
            _allSongs.Clear();
            Playlists.Clear();

            if (songs == null) return;

            foreach (var song in songs)
            {
                _allSongs.Add(song);
            }

            // Agrupar canciones por género
            var genreGroups = _allSongs
                .GroupBy(s => string.IsNullOrEmpty(s.Genre) ? "Sin Género" : s.Genre)
                .OrderBy(g => g.Key);

            // Crear playlist "Todas las canciones"
            var allSongsPlaylist = new Playlist("Todas", _allSongs)
            {
                Name = "🎵 Todas las canciones"
            };
            Playlists.Add(allSongsPlaylist);

            // Crear playlists por género
            foreach (var genreGroup in genreGroups)
            {
                var playlist = new Playlist(genreGroup.Key, genreGroup.ToList());
                Playlists.Add(playlist);
            }

            // Crear playlist de favoritos (ejemplo)
            CreateSpecialPlaylists();

            // Seleccionar la primera playlist por defecto
            if (Playlists.Count > 0)
            {
                SelectedPlaylist = Playlists[0];
            }
        }

        /// <summary>
        /// Crea listas de reproducción especiales
        /// </summary>
        private void CreateSpecialPlaylists()
        {
            // Playlist de canciones recientes (últimas 50 canciones agregadas)
            var recentSongs = _allSongs.OrderByDescending(s => s.DateAdded).Take(50);
            if (recentSongs.Any())
            {
                var recentPlaylist = new Playlist("Recientes", recentSongs)
                {
                    Name = "🕐 Agregadas Recientemente"
                };
                Playlists.Add(recentPlaylist);
            }

            // Playlist de canciones más largas
            var longSongs = _allSongs.Where(s => s.Duration.TotalMinutes > 5).OrderByDescending(s => s.Duration);
            if (longSongs.Any())
            {
                var longPlaylist = new Playlist("Largas", longSongs)
                {
                    Name = "⏱️ Canciones Largas"
                };
                Playlists.Add(longPlaylist);
            }
        }

        /// <summary>
        /// Actualiza las listas cuando se agregan nuevas canciones
        /// </summary>
        /// <param name="newSongs">Nuevas canciones</param>
        public void UpdatePlaylists(IEnumerable<Song> newSongs)
        {
            GeneratePlaylistsFromSongs(newSongs);
        }

        /// <summary>
        /// Busca canciones en todas las listas
        /// </summary>
        /// <param name="searchTerm">Término de búsqueda</param>
        /// <returns>Canciones que coinciden con la búsqueda</returns>
        public ObservableCollection<Song> SearchSongs(string searchTerm)
        {
            if (string.IsNullOrWhiteSpace(searchTerm))
                return new ObservableCollection<Song>(_allSongs);

            var results = _allSongs.Where(s =>
                s.Title.Contains(searchTerm, StringComparison.OrdinalIgnoreCase) ||
                s.Artist.Contains(searchTerm, StringComparison.OrdinalIgnoreCase) ||
                s.Album.Contains(searchTerm, StringComparison.OrdinalIgnoreCase) ||
                s.Genre.Contains(searchTerm, StringComparison.OrdinalIgnoreCase)
            ).ToList();

            return new ObservableCollection<Song>(results);
        }

        /// <summary>
        /// Obtiene estadísticas de la biblioteca musical
        /// </summary>
        /// <returns>Diccionario con estadísticas</returns>
        public Dictionary<string, object> GetLibraryStats()
        {
            var stats = new Dictionary<string, object>();

            stats["TotalSongs"] = _allSongs.Count;
            stats["TotalPlaylists"] = Playlists.Count;
            stats["TotalGenres"] = _allSongs.GroupBy(s => s.Genre).Count();
            stats["TotalArtists"] = _allSongs.GroupBy(s => s.Artist).Count();
            stats["TotalDuration"] = TimeSpan.FromSeconds(_allSongs.Sum(s => s.Duration.TotalSeconds));

            return stats;
        }

        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}