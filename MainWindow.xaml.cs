using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using StunsCat.ViewModels;

namespace StunsCat
{
    public partial class MainWindow : Window
    {
        private MainViewModel _viewModel;
        private bool _isMaximized = false;
        private WindowState _previousWindowState;
        private double _previousWidth;
        private double _previousHeight;
        private double _previousLeft;
        private double _previousTop;

        public MainWindow()
        {
            InitializeComponent();
            _viewModel = new MainViewModel();
            DataContext = _viewModel;

            // Permitir arrastrar la ventana
            MouseDown += MainWindow_MouseDown;

            // Suscribirse a cambios en el GIF de fondo
            _viewModel.PropertyChanged += ViewModel_PropertyChanged;

            // Inicializar el estado de la ventana
            _previousWindowState = WindowState;
            _previousWidth = Width;
            _previousHeight = Height;
            _previousLeft = Left;
            _previousTop = Top;
        }

        private void MainWindow_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed)
            {
                DragMove();
            }
        }

        private void ViewModel_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(MainViewModel.CurrentBackgroundGif))
            {
                System.Diagnostics.Debug.WriteLine("🎯 Se detectó cambio en CurrentBackgroundGif");
                UpdateBackgroundGif();
            }
        }

        private void UpdateBackgroundGif()
        {
            if (!string.IsNullOrEmpty(_viewModel.CurrentBackgroundGif))
            {
                try
                {
                    System.Diagnostics.Debug.WriteLine($"🎭 Asignando GIF a Image: {_viewModel.CurrentBackgroundGif}");

                    var gifImage = new BitmapImage();
                    gifImage.BeginInit();
                    gifImage.UriSource = new Uri(_viewModel.CurrentBackgroundGif, UriKind.RelativeOrAbsolute);
                    gifImage.CacheOption = BitmapCacheOption.OnLoad;
                    gifImage.EndInit();

                    // Buscar el control Image en el XAML (necesitarás agregarlo)
                    // Suponiendo que tienes un control Image llamado "BackgroundGif"
                    BackgroundGif.Source = gifImage;

                    System.Diagnostics.Debug.WriteLine($"🎬 GIF cargado correctamente");
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Error loading background GIF: {ex.Message}");
                }
            }
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            _viewModel.Dispose();
            Application.Current.Shutdown();
        }

        private void MinimizeButton_Click(object sender, RoutedEventArgs e)
        {
            WindowState = WindowState.Minimized;
        }

        private void MaximizeButton_Click(object sender, RoutedEventArgs e)
        {
            if (_isMaximized)
            {
                WindowState = WindowState.Normal;
                Width = _previousWidth;
                Height = _previousHeight;
                Left = _previousLeft;
                Top = _previousTop;
                _isMaximized = false;
            }
            else
            {
                _previousWidth = Width;
                _previousHeight = Height;
                _previousLeft = Left;
                _previousTop = Top;
                WindowState = WindowState.Maximized;
                _isMaximized = true;
            }
        }

        private bool _isUserDragging = false;

        private void ProgressSlider_PreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            _isUserDragging = true;
        }

        private void ProgressSlider_PreviewMouseUp(object sender, MouseButtonEventArgs e)
        {
            _isUserDragging = false;

            if (sender is Slider slider)
            {
                var newPosition = TimeSpan.FromSeconds(slider.Value);
                _viewModel.SeekToPosition(newPosition);
            }
        }

        private void ProgressSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (_isUserDragging && sender is Slider slider)
            {
                var newPosition = TimeSpan.FromSeconds(slider.Value);
                _viewModel.SeekToPosition(newPosition);
            }
        }

        protected override void OnClosing(CancelEventArgs e)
        {
            _viewModel.Dispose();
            base.OnClosing(e);
        }

        private void ProgressBar_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            // Implement logic for handling the value change of the ProgressBar
        }
    }
}