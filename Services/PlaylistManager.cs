using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using StunsCat.Models;

namespace StunsCat.Services
{
    public class PlaylistManager : INotifyPropertyChanged
    {
        private ObservableCollection<Playlist> _playlists;
        private Playlist _selectedPlaylist;

        public ObservableCollection<Playlist> Playlists
        {
            get => _playlists;
            set => SetProperty(ref _playlists, value);
        }

        public Playlist SelectedPlaylist
        {
            get => _selectedPlaylist;
            set
            {
                // Deseleccionar playlist anterior
                if (_selectedPlaylist != null)
                    _selectedPlaylist.IsSelected = false;

                SetProperty(ref _selectedPlaylist, value);

                // Seleccionar nueva playlist
                if (_selectedPlaylist != null)
                    _selectedPlaylist.IsSelected = true;
            }
        }

        public PlaylistManager()
        {
            Playlists = new ObservableCollection<Playlist>();
        }

        public void CreatePlaylistsFromSongs(IEnumerable<Song> songs)
        {
            if (songs == null) return;

            var songList = songs.ToList();
            if (!songList.Any()) return;

            // Limpiar playlists existentes
            Playlists.Clear();

            // Agrupar por género
            var genreGroups = songList
                .GroupBy(s => string.IsNullOrWhiteSpace(s.Genre) ? "Sin Género" : s.Genre.Trim())
                .OrderBy(g => g.Key);

            foreach (var genreGroup in genreGroups)
            {
                var playlist = new Playlist(genreGroup.Key, genreGroup.OrderBy(s => s.Artist).ThenBy(s => s.Title));
                Playlists.Add(playlist);
            }

            // Crear playlist "Todas las canciones"
            var allSongsPlaylist = new Playlist("Todas", songList.OrderBy(s => s.Artist).ThenBy(s => s.Title))
            {
                Name = "Todas las Canciones"
            };
            Playlists.Insert(0, allSongsPlaylist);

            // Seleccionar la primera playlist por defecto
            if (Playlists.Any())
            {
                SelectedPlaylist = Playlists.First();
            }

            System.Diagnostics.Debug.WriteLine($"✅ Creadas {Playlists.Count} playlists con {songList.Count} canciones total");
        }

        public void AddSongToPlaylist(string playlistName, Song song)
        {
            if (song == null || string.IsNullOrWhiteSpace(playlistName)) return;

            var playlist = Playlists.FirstOrDefault(p =>
                string.Equals(p.Name, playlistName, StringComparison.OrdinalIgnoreCase));

            if (playlist == null)
            {
                // Crear nueva playlist
                playlist = new Playlist(playlistName, new[] { song })
                {
                    Name = playlistName
                };
                Playlists.Add(playlist);
            }
            else
            {
                // Agregar a playlist existente si no está ya
                if (!playlist.Songs.Contains(song))
                {
                    playlist.Songs.Add(song);
                }
            }
        }

        public void RemoveSongFromPlaylist(string playlistName, Song song)
        {
            if (song == null || string.IsNullOrWhiteSpace(playlistName)) return;

            var playlist = Playlists.FirstOrDefault(p =>
                string.Equals(p.Name, playlistName, StringComparison.OrdinalIgnoreCase));

            if (playlist != null && playlist.Songs.Contains(song))
            {
                playlist.Songs.Remove(song);

                // Si la playlist queda vacía (excepto "Todas las Canciones"), eliminarla
                if (playlist.Songs.Count == 0 && playlist.Name != "Todas las Canciones")
                {
                    Playlists.Remove(playlist);
                }
            }
        }

        public Playlist CreateCustomPlaylist(string name, IEnumerable<Song> songs = null)
        {
            if (string.IsNullOrWhiteSpace(name)) return null;

            // Verificar que no exista ya una playlist con ese nombre
            if (Playlists.Any(p => string.Equals(p.Name, name, StringComparison.OrdinalIgnoreCase)))
            {
                return null; // Ya existe
            }

            var playlist = new Playlist("Personalizada", songs ?? new List<Song>())
            {
                Name = name
            };

            Playlists.Add(playlist);
            return playlist;
        }

        public void DeletePlaylist(Playlist playlist)
        {
            if (playlist == null || playlist.Name == "Todas las Canciones") return;

            if (Playlists.Contains(playlist))
            {
                if (SelectedPlaylist == playlist)
                {
                    SelectedPlaylist = Playlists.FirstOrDefault(p => p != playlist);
                }

                Playlists.Remove(playlist);
            }
        }

        public List<Song> GetAllSongs()
        {
            var allSongs = new List<Song>();

            foreach (var playlist in Playlists)
            {
                foreach (var song in playlist.Songs)
                {
                    if (!allSongs.Contains(song))
                    {
                        allSongs.Add(song);
                    }
                }
            }

            return allSongs;
        }

        public void RefreshPlaylists()
        {
            var allSongs = GetAllSongs();
            CreatePlaylistsFromSongs(allSongs);
        }

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
            return true;
        }

        #endregion
    }
}