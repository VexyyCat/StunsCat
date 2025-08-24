using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;
using StunsCat.Models;
using TagLib;

namespace StunsCat.Services
{
    public class MusicScanService : IDisposable
    {
        private readonly string[] _supportedFormats = { ".mp3", ".flac", ".wav", ".m4a", ".ogg", ".wma", ".aac" };
        private bool _disposed = false;

        public event EventHandler<ScanProgressEventArgs> ScanProgress;
        public event EventHandler<string> ScanStatusChanged;

        public async Task<List<Song>> ScanDirectoryAsync(string directoryPath)
        {
            var songs = new List<Song>();

            if (string.IsNullOrEmpty(directoryPath))
            {
                throw new ArgumentException("La ruta del directorio no puede ser nula o vacía.", nameof(directoryPath));
            }

            if (!Directory.Exists(directoryPath))
            {
                throw new DirectoryNotFoundException($"La carpeta {directoryPath} no existe.");
            }

            OnScanStatusChanged("Iniciando escaneo...");

            try
            {
                var files = Directory.GetFiles(directoryPath, "*.*", SearchOption.AllDirectories)
                    .Where(file => _supportedFormats.Contains(Path.GetExtension(file).ToLower()))
                    .ToList();

                int totalFiles = files.Count;
                int processedFiles = 0;

                if (totalFiles == 0)
                {
                    OnScanStatusChanged("No se encontraron archivos de audio en la carpeta especificada.");
                    return songs;
                }

                OnScanStatusChanged($"Se encontraron {totalFiles} archivos de audio. Procesando...");

                foreach (var filePath in files)
                {
                    if (_disposed) break;

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

                OnScanStatusChanged($"Escaneo completado. Se encontraron {songs.Count} canciones válidas de {totalFiles} archivos procesados.");
            }
            catch (UnauthorizedAccessException ex)
            {
                var errorMsg = $"Sin permisos para acceder a la carpeta: {ex.Message}";
                OnScanStatusChanged(errorMsg);
                throw;
            }
            catch (Exception ex)
            {
                var errorMsg = $"Error durante el escaneo: {ex.Message}";
                OnScanStatusChanged(errorMsg);
                throw;
            }

            return songs;
        }

        private async Task<Song> ProcessAudioFileAsync(string filePath)
        {
            if (_disposed) return null;

            return await Task.Run(() =>
            {
                try
                {
                    if (!System.IO.File.Exists(filePath)) // Specify System.IO.File to resolve ambiguity
                    {
                        return null;
                    }

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
                    using var file = TagLib.File.Create(filePath); // Specify TagLib.File to resolve ambiguity
                    var tag = file.Tag;
                    var properties = file.Properties;

                    // Metadata básica
                    song.Title = !string.IsNullOrEmpty(tag.Title) ? tag.Title.Trim() : Path.GetFileNameWithoutExtension(filePath);
                    song.Artist = !string.IsNullOrEmpty(tag.FirstPerformer) ? tag.FirstPerformer.Trim() : "Artista desconocido";
                    song.Album = !string.IsNullOrEmpty(tag.Album) ? tag.Album.Trim() : "Álbum desconocido";
                    song.Genre = !string.IsNullOrEmpty(tag.FirstGenre) ? tag.FirstGenre.Trim() : "Sin género";
                    song.Year = tag.Year > 0 ? (int)tag.Year : 0;
                    song.Comment = tag.Comment?.Trim() ?? string.Empty;

                    // Propiedades de audio
                    song.Duration = properties?.Duration ?? TimeSpan.Zero;
                    song.Bitrate = properties?.AudioBitrate ?? 0;
                    song.SampleRate = properties?.AudioSampleRate ?? 0;

                    // Intentar obtener BPM
                    song.BPM = ExtractBPM(tag);

                    // Extraer carátula del álbum
                    song.AlbumArt = ExtractAlbumArt(tag);
                    return song;
                }
                catch (TagLib.CorruptFileException ex)
                {
                    return null;
                }
                catch (TagLib.UnsupportedFormatException ex)
                {
                    return null;
                }
                catch (UnauthorizedAccessException ex)
                {
                    return null;
                }
                catch (Exception ex)
                {
                    return null;
                }
            });
        }

        private int ExtractBPM(Tag tag)
        {
            try
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
                        var words = beforeBpm.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                        if (words.Length > 0 && int.TryParse(words.Last(), out int bpm) && bpm > 0 && bpm <= 300)
                            return bpm;
                    }

                    // Buscar patrones como "120 BPM" o "BPM: 120"
                    var patterns = new[] { @"(\d+)\s*bpm", @"bpm\s*:?\s*(\d+)" };
                    foreach (var pattern in patterns)
                    {
                        var match = System.Text.RegularExpressions.Regex.Match(comment, pattern);
                        if (match.Success && int.TryParse(match.Groups[1].Value, out int bpmFromPattern)
                            && bpmFromPattern > 0 && bpmFromPattern <= 300)
                            return bpmFromPattern;
                    }
                }

                // Si no se encuentra, estimar basado en el género
                return EstimateBPMByGenre(tag.FirstGenre);
            }
            catch (Exception ex)
            {
                return EstimateBPMByGenre(tag?.FirstGenre);
            }
        }

        private int EstimateBPMByGenre(string genre)
        {
            if (string.IsNullOrEmpty(genre))
                return 120; // BPM promedio

            var genreLower = genre.ToLower().Trim();
            return genreLower switch
            {
                var g when g.Contains("ballad") || g.Contains("blues") => 80,
                var g when g.Contains("jazz") => 90,
                var g when g.Contains("rock") => 120,
                var g when g.Contains("pop") => 110,
                var g when g.Contains("metal") => 140,
                var g when g.Contains("electronic") || g.Contains("edm") || g.Contains("dance") => 128,
                var g when g.Contains("hip hop") || g.Contains("rap") => 100,
                var g when g.Contains("reggae") => 75,
                var g when g.Contains("country") => 95,
                var g when g.Contains("classical") => 80,
                var g when g.Contains("ambient") => 70,
                var g when g.Contains("techno") => 130,
                var g when g.Contains("house") => 125,
                var g when g.Contains("trance") => 135,
                var g when g.Contains("drum") && g.Contains("bass") => 170,
                var g when g.Contains("punk") => 150,
                var g when g.Contains("folk") => 85,
                _ => 120
            };
        }

        private BitmapImage ExtractAlbumArt(Tag tag)
        {
            try
            {
                var pictures = tag?.Pictures;
                if (pictures != null && pictures.Length > 0)
                {
                    var picture = pictures[0];
                    if (picture.Data?.Data != null && picture.Data.Data.Length > 0)
                    {
                        var imageData = picture.Data.Data;

                        var bitmapImage = new BitmapImage();
                        bitmapImage.BeginInit();
                        bitmapImage.StreamSource = new MemoryStream(imageData);
                        bitmapImage.CacheOption = BitmapCacheOption.OnLoad;
                        bitmapImage.DecodePixelWidth = 300; // Optimizar tamaño para rendimiento
                        bitmapImage.DecodePixelHeight = 300;
                        bitmapImage.EndInit();
                        bitmapImage.Freeze(); // Para uso en múltiples hilos

                        return bitmapImage;
                    }
                }
            }
            catch (Exception ex)
            {
            }

            return null;
        }

        public bool IsAudioFile(string filePath)
        {
            if (string.IsNullOrEmpty(filePath))
                return false;

            try
            {
                var extension = Path.GetExtension(filePath)?.ToLower();
                return !string.IsNullOrEmpty(extension) && _supportedFormats.Contains(extension);
            }
            catch (Exception ex)
            {
                return false;
            }
        }

        public string[] GetSupportedFormats()
        {
            return _supportedFormats.ToArray();
        }

        protected virtual void OnScanProgress(ScanProgressEventArgs e)
        {
            if (_disposed) return;

            try
            {
                ScanProgress?.Invoke(this, e);
            }
            catch (Exception ex)
            {
            }
        }

        protected virtual void OnScanStatusChanged(string status)
        {
            if (_disposed) return;

            try
            {
                ScanStatusChanged?.Invoke(this, status);
            }
            catch (Exception ex)
            {
            }
        }

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
                    // Limpiar eventos
                    ScanProgress = null;
                    ScanStatusChanged = null;
                }
                _disposed = true;
            }
        }

        #endregion
    }

    public class ScanProgressEventArgs : EventArgs
    {
        public double ProgressPercentage { get; }
        public int ProcessedFiles { get; }
        public int TotalFiles { get; }
        public string CurrentFile { get; }

        public ScanProgressEventArgs(double progressPercentage, int processedFiles, int totalFiles, string currentFile)
        {
            ProgressPercentage = Math.Max(0, Math.Min(100, progressPercentage));
            ProcessedFiles = Math.Max(0, processedFiles);
            TotalFiles = Math.Max(0, totalFiles);
            CurrentFile = currentFile ?? string.Empty;
        }

        public override string ToString()
        {
            return $"Progreso: {ProgressPercentage:F1}% ({ProcessedFiles}/{TotalFiles}) - {CurrentFile}";
        }
    }
}