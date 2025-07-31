using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace StunsCat.Models
{
    public class Playlist : INotifyPropertyChanged
    {
        public string Name
        {
            get
            {
                return this._name;
            }
            set
            {
                this._name = value;
                this.OnPropertyChanged("Name");
            }
        }

        public string Genre
        {
            get
            {
                return this._genre;
            }
            set
            {
                this._genre = value;
                this.OnPropertyChanged("Genre");
            }
        }

        public int SongCount
        {
            get
            {
                return this._songCount;
            }
            set
            {
                this._songCount = value;
                this.OnPropertyChanged("SongCount");
            }
        }

        public string TotalDuration
        {
            get
            {
                return this._totalDuration;
            }
            set
            {
                this._totalDuration = value;
                this.OnPropertyChanged("TotalDuration");
            }
        }

        public ObservableCollection<Song> Songs
        {
            get
            {
                return this._songs;
            }
            set
            {
                this._songs = value;
                this.OnPropertyChanged("Songs");
                this.UpdatePlaylistInfo();
            }
        }

        public bool IsSelected
        {
            get
            {
                return this._isSelected;
            }
            set
            {
                this._isSelected = value;
                this.OnPropertyChanged("IsSelected");
            }
        }

        public string PlaylistIcon
        {
            get
            {
                string genre = this.Genre?.ToLower();

                return genre switch
                {
                    "folk" => "🪕",
                    "ambient" => "🌙",
                    "jazz" => "🎷",
                    "rock" => "🎸",
                    "r&b" => "🎵",
                    "pop" => "🎤",
                    "electronic" => "🎧",
                    "latin" => "💃",
                    "classical" => "🎼",
                    "country" => "🤠",
                    "hip hop" => "🎤",
                    "metal" => "🤘",
                    "reggae" => "🌴",
                    "dance" => "💃",
                    "blues" => "🎺",
                    _ => "🎵"
                };
            }
        }

        public Playlist()
        {
            this.Songs = new ObservableCollection<Song>();
            this.Songs.CollectionChanged += this.Songs_CollectionChanged;
        }

        public Playlist(string genre, IEnumerable<Song> songs) : this()
        {
            this.Genre = genre;
            this.Name = (string.IsNullOrEmpty(genre) ? "Sin Género" : genre);
            foreach (Song song in songs)
            {
                this.Songs.Add(song);
            }
            this.UpdatePlaylistInfo();
        }

        private void Songs_CollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            this.UpdatePlaylistInfo();
        }

        private void UpdatePlaylistInfo()
        {
            if (this.Songs != null)
            {
                this.SongCount = this.Songs.Count;
                TimeSpan totalTime = TimeSpan.Zero;
                foreach (Song song in this.Songs)
                {
                    totalTime = totalTime.Add(song.Duration);
                }
                this.TotalDuration = this.FormatDuration(totalTime);
            }
        }

        private string FormatDuration(TimeSpan duration)
        {
            if (duration.TotalHours >= 1.0)
            {
                return $"{(int)duration.TotalHours:D2}:{duration.Minutes:D2}:{duration.Seconds:D2}";
            }
            else
            {
                return $"{duration.Minutes:D2}:{duration.Seconds:D2}";
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChangedEventHandler propertyChanged = this.PropertyChanged;
            propertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        private string _name;
        private string _genre;
        private int _songCount;
        private string _totalDuration;
        private ObservableCollection<Song> _songs;
        private bool _isSelected;
    }
}