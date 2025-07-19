using System.IO;
using System.Windows.Media.Imaging;
using StunsCat.Models;
using TagLib;

namespace StunsCat.Services
{
    public class MusicScanService
    {
        private readonly string[] supportedFormats = { ".mp3", ".flac", ".wav", ".m4a", ".ogg", ".wma", ".aac", ".opus" };

        public event EventHandler<ScanProgressEventArgs> ScanProgress;
        public event EventHandler<string> ScanStatusChanged;

        public async Task<List<Song>> ScanDirectoryAsync(string directoryPath)
        {
            var songs = new List<Song>();

            if (!Directory.Exists(directoryPath))
            {
                throw new DirectoryNotFoundException($"La carpeta {directoryPath} no existe.");
            }

            OnScanStatusChanged("Iniciando escaneo...");

            var files = Directory.GetFiles(directoryPath, "*.*", SearchOption.AllDirectories)
                .Where(file => supportedFormats.Contains(Path.GetExtension(file).ToLower()))
                .ToList();

            int totalFiles = files.Count;
            int processedFiles = 0;

            foreach (var filePath in files)
            {
                try
                {
                    var song = await ProcessAudioFileAsync(filePath);
                    if (song != null)
                    {
                        songs.Add(song);
                    }
                }
                catch (Exception ex)
                {
                    OnScanStatusChanged($"Error procesando {Path.GetFileName(filePath)}: {ex.Message}");
                }

                processedFiles++;
                var progress = (double)processedFiles / totalFiles * 100;
                OnScanProgress(new ScanProgressEventArgs(progress, processedFiles, totalFiles, Path.GetFileName(filePath)));

                // Permitir que la UI se actualice
                await Task.Delay(1);
            }

            OnScanStatusChanged($"Escaneo completado. Se encontraron {songs.Count} canciones.");
            return songs;
        }

        private async Task<Song> ProcessAudioFileAsync(string filePath)
        {
            return await Task.Run(() =>
            {
                try
                {
                    var fileInfo = new FileInfo(filePath);
                    var song = new Song
                    {
                        FilePath = filePath,
                        FileName = fileInfo.Name,
                        DateAdded = DateTime.Now,
                        FileSize = fileInfo.Length,
                        Format = Path.GetExtension(filePath).ToUpper().Substring(1)
                    };

                    // Leer metadata con TagLib
                    using var file = TagLib.File.Create(filePath);
                    var tag = file.Tag;
                    var properties = file.Properties;

                    song.Title = !string.IsNullOrEmpty(tag.Title) ? tag.Title : Path.GetFileNameWithoutExtension(filePath);
                    song.Artist = !string.IsNullOrEmpty(tag.FirstPerformer) ? tag.FirstPerformer : "Artista desconocido";
                    song.Album = !string.IsNullOrEmpty(tag.Album) ? tag.Album : "Álbum desconocido";
                    song.Genre = tag.FirstGenre ?? "Sin género";
                    song.Year = (int)tag.Year;
                    song.Comment = tag.Comment;
                    song.Duration = properties.Duration;
                    song.Bitrate = properties.AudioBitrate;
                    song.SampleRate = properties.AudioSampleRate;

                    // Intentar obtener BPM del comentario o de tags personalizados
                    song.BPM = ExtractBPM(tag);

                    // Extraer carátula del álbum
                    song.AlbumArt = ExtractAlbumArt(tag);

                    return song;
                }
                catch (Exception ex)
                {
                    OnScanStatusChanged($"Error procesando {Path.GetFileName(filePath)}: {ex.Message}");
                    return null;
                }
            });
        }

        private int ExtractBPM(Tag tag)
        {
            // Intentar extraer BPM de diferentes fuentes
            if (tag.BeatsPerMinute > 0)
                return (int)tag.BeatsPerMinute;

            // Buscar en el comentario
            if (!string.IsNullOrEmpty(tag.Comment))
            {
                var comment = tag.Comment.ToLower();
                var bpmIndex = comment.IndexOf("bpm");
                if (bpmIndex > 0)
                {
                    var beforeBpm = comment.Substring(0, bpmIndex).Trim();
                    var words = beforeBpm.Split(' ');
                    if (words.Length > 0 && int.TryParse(words.Last(), out int bpm))
                        return bpm;
                }
            }

            // Si no se encuentra, estimar basado en el género
            return EstimateBPMByGenre(tag.FirstGenre);
        }

        private int EstimateBPMByGenre(string genre)
        {
            if (string.IsNullOrEmpty(genre))
                return 120; // BPM promedio

            var genreLower = genre.ToLower();
            return genreLower switch
            {
                var g when g.Contains("ballad") || g.Contains("blues") => 80,
                var g when g.Contains("jazz") => 90,
                var g when g.Contains("rock") => 120,
                var g when g.Contains("pop") => 110,
                var g when g.Contains("metal") => 140,
                var g when g.Contains("electronic") || g.Contains("edm") => 128,
                var g when g.Contains("hip hop") || g.Contains("rap") => 100,
                var g when g.Contains("reggae") => 75,
                var g when g.Contains("country") => 95,
                var g when g.Contains("classical") => 80,
                _ => 120
            };
        }

        private BitmapImage ExtractAlbumArt(Tag tag)
        {
            try
            {
                var pictures = tag.Pictures;
                if (pictures.Length > 0)
                {
                    var picture = pictures[0];
                    var imageData = picture.Data.Data;

                    var bitmapImage = new BitmapImage();
                    bitmapImage.BeginInit();
                    bitmapImage.StreamSource = new MemoryStream(imageData);
                    bitmapImage.CacheOption = BitmapCacheOption.OnLoad;
                    bitmapImage.EndInit();
                    bitmapImage.Freeze();

                    return bitmapImage;
                }
            }
            catch (Exception ex)
            {
                OnScanStatusChanged($"Error extrayendo carátula: {ex.Message}");
            }

            return null;
        }

        public bool IsAudioFile(string filePath)
        {
            var extension = Path.GetExtension(filePath).ToLower();
            return supportedFormats.Contains(extension);
        }

        protected virtual void OnScanProgress(ScanProgressEventArgs e)
        {
            ScanProgress?.Invoke(this, e);
        }

        protected virtual void OnScanStatusChanged(string status)
        {
            ScanStatusChanged?.Invoke(this, status);
        }
    }

    public class ScanProgressEventArgs : EventArgs
    {
        public double ProgressPercentage { get; }
        public int ProcessedFiles { get; }
        public int TotalFiles { get; }
        public string CurrentFile { get; }

        public ScanProgressEventArgs(double progressPercentage, int processedFiles, int totalFiles, string currentFile)
        {
            ProgressPercentage = progressPercentage;
            ProcessedFiles = processedFiles;
            TotalFiles = totalFiles;
            CurrentFile = currentFile;
        }
    }
}