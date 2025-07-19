using System.IO;
using System.Windows.Media.Imaging;

namespace StunsCat.Models
{
    public class Song
    {
        public string FilePath { get; set; }
        public string FileName { get; set; }
        public string Title { get; set; }
        public string Artist { get; set; }
        public string Album { get; set; }
        public string Genre { get; set; }
        public int Year { get; set; }
        public string Comment { get; set; }
        public TimeSpan Duration { get; set; }
        public int Bitrate { get; set; }
        public int SampleRate { get; set; }
        public int BPM { get; set; }
        public BitmapImage AlbumArt { get; set; }
        public DateTime DateAdded { get; set; }
        public long FileSize { get; set; }
        public string Format { get; set; }

        // Método actualizado para obtener GIF de fondo basado en BPM  
        public string GetBackgroundGifByBPM()
        {
            string gifName = BPM switch
            {
                < 80 => "slow_bg.gif",        // Música lenta (baladas, blues)  
                >= 80 and < 100 => "medium_bg.gif",  // Música moderada (jazz, reggae)  
                >= 100 and < 120 => "normal_bg.gif", // Música normal (pop, hip hop)  
                >= 120 and < 140 => "fast_bg.gif",   // Música rápida (rock, electronic)  
                _ => "default_bg.gif"                // Música muy rápida (metal, hardcore)  
            };

            return Path.GetFullPath($"Assets/Gifs/{gifName}");
        }

        // Propiedades de solo lectura para mostrar información formateada  
        public string DurationFormatted => Duration.ToString(@"mm\:ss");
        public string FileSizeFormatted => FormatFileSize(FileSize);
        public string BitrateFormatted => $"{Bitrate} kbps";
        public string SampleRateFormatted => $"{SampleRate} Hz";
        public string BPMFormatted => $"{BPM} BPM";

        private string FormatFileSize(long bytes)
        {
            const int scale = 1024;
            string[] orders = { "GB", "MB", "KB", "Bytes" };
            long max = (long)Math.Pow(scale, orders.Length - 1);

            foreach (string order in orders)
            {
                if (bytes > max)
                    return $"{decimal.Divide(bytes, max):##.##} {order}";

                max /= scale;
            }
            return "0 Bytes";
        }

        public override string ToString()
        {
            return $"{Artist} - {Title}";
        }

        // Add these properties to fix the error  
        public bool IsPlaying { get; set; }
        public bool IsPaused { get; set; }
    }
}