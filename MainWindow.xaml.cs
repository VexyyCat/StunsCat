using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
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
        private bool _isUserDragging = false;
        private Storyboard _vinylRotationStoryboard;
        private DispatcherTimer _uiUpdateTimer;
        private bool _isDisposed = false;

        public MainWindow()
        {
            try
            {
                InitializeComponent();
                InitializeViewModel();
                SetupEvents();
                InitializeWindowState();
                SetupAnimations();
                InitializeUIUpdateTimer();
                LoadInitialConfiguration();

                System.Diagnostics.Debug.WriteLine("✅ MainWindow inicializada correctamente");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"❌ Error inicializando MainWindow: {ex.Message}");
                MessageBox.Show($"Error inicializando la aplicación: {ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        #region Inicialización

        private void InitializeViewModel()
        {
            try
            {
                _viewModel = new MainViewModel();
                DataContext = _viewModel;
                System.Diagnostics.Debug.WriteLine("✅ ViewModel inicializado y asignado");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"❌ Error inicializando ViewModel: {ex.Message}");
                throw;
            }
        }

        private void SetupEvents()
        {
            try
            {
                // Permitir arrastrar la ventana
                MouseDown += MainWindow_MouseDown;

                // Suscribirse a cambios en el ViewModel
                if (_viewModel != null)
                {
                    _viewModel.PropertyChanged += ViewModel_PropertyChanged;
                }

                // Eventos de teclado
                KeyDown += MainWindow_KeyDown;

                // Evento de cierre
                Closing += MainWindow_Closing;

                // Eventos de ventana
                StateChanged += MainWindow_StateChanged;
                SizeChanged += MainWindow_SizeChanged;

                System.Diagnostics.Debug.WriteLine("✅ Eventos configurados");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"❌ Error configurando eventos: {ex.Message}");
            }
        }

        private void InitializeWindowState()
        {
            try
            {
                _previousWindowState = WindowState;
                _previousWidth = Width;
                _previousHeight = Height;
                _previousLeft = Left;
                _previousTop = Top;

                // Centrar la ventana inicialmente
                CenterWindow();

                System.Diagnostics.Debug.WriteLine("✅ Estado de ventana inicializado");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"❌ Error inicializando estado de ventana: {ex.Message}");
            }
        }

        private void SetupAnimations()
        {
            try
            {
                // Configurar la animación del vinilo
                _vinylRotationStoryboard = (Storyboard)FindResource("VinylRotationStoryboard");

                if (_vinylRotationStoryboard != null)
                {
                    System.Diagnostics.Debug.WriteLine("✅ Animación del vinilo configurada");
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine("⚠️ No se encontró la animación del vinilo en los recursos");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"❌ Error configurando animaciones: {ex.Message}");
            }
        }

        private void InitializeUIUpdateTimer()
        {
            try
            {
                // Timer para actualizaciones periódicas de UI
                _uiUpdateTimer = new DispatcherTimer
                {
                    Interval = TimeSpan.FromMilliseconds(500) // Actualizar cada 500ms
                };
                _uiUpdateTimer.Tick += UIUpdateTimer_Tick;
                _uiUpdateTimer.Start();

                System.Diagnostics.Debug.WriteLine("✅ Timer de actualización de UI inicializado");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"❌ Error inicializando timer de UI: {ex.Message}");
            }
        }

        private void LoadInitialConfiguration()
        {
            try
            {
                // Cargar configuración guardada si existe
                _viewModel?.LoadConfiguration();
                System.Diagnostics.Debug.WriteLine("✅ Configuración inicial cargada");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"❌ Error cargando configuración inicial: {ex.Message}");
            }
        }

        #endregion

        #region Timer de Actualización de UI

        private void UIUpdateTimer_Tick(object sender, EventArgs e)
        {
            if (_isDisposed || _viewModel == null)
                return;

            try
            {
                // Forzar actualización de comandos para actualizar el estado de habilitado/deshabilitado
                CommandManager.InvalidateRequerySuggested();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"❌ Error en timer de actualización de UI: {ex.Message}");
            }
        }

        #endregion

        #region Eventos del ViewModel

        private void ViewModel_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (_isDisposed)
                return;

            try
            {
                // Asegurar que se ejecute en el hilo de UI
                if (!Dispatcher.CheckAccess())
                {
                    Dispatcher.BeginInvoke(() => ViewModel_PropertyChanged(sender, e));
                    return;
                }

                switch (e.PropertyName)
                {
                    case nameof(MainViewModel.CurrentBackgroundGif):
                        System.Diagnostics.Debug.WriteLine("🎯 Se detectó cambio en CurrentBackgroundGif");
                        UpdateBackgroundGif();
                        break;

                    case nameof(MainViewModel.IsVinylRotating):
                        UpdateVinylAnimation();
                        break;

                    case nameof(MainViewModel.CurrentSong):
                        UpdateWindowTitle();
                        break;

                    case nameof(MainViewModel.IsSongLoading):
                        UpdateLoadingState();
                        break;

                    case nameof(MainViewModel.IsScanning):
                        UpdateScanningState();
                        break;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"❌ Error manejando PropertyChanged: {ex.Message}");
            }
        }

        #endregion

        #region Actualización de UI

        private void UpdateBackgroundGif()
        {
            try
            {
                if (!string.IsNullOrEmpty(_viewModel.CurrentBackgroundGif))
                {
                    System.Diagnostics.Debug.WriteLine($"🎭 Asignando GIF a Image: {_viewModel.CurrentBackgroundGif}");

                    var gifImage = new BitmapImage();
                    gifImage.BeginInit();
                    gifImage.UriSource = new Uri(_viewModel.CurrentBackgroundGif, UriKind.RelativeOrAbsolute);
                    gifImage.CacheOption = BitmapCacheOption.OnLoad;
                    gifImage.EndInit();
                    gifImage.Freeze(); // Mejorar performance

                    // Verificar que el control existe antes de asignar
                    if (BackgroundGif != null)
                    {
                        BackgroundGif.Source = gifImage;
                        System.Diagnostics.Debug.WriteLine($"🎬 GIF cargado correctamente");
                    }
                }
                else
                {
                    // Limpiar la imagen si no hay GIF seleccionado
                    if (BackgroundGif != null)
                    {
                        BackgroundGif.Source = null;
                        System.Diagnostics.Debug.WriteLine("🧹 GIF de fondo limpiado");
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"❌ Error loading background GIF: {ex.Message}");

                // Fallback: limpiar la imagen si hay error
                if (BackgroundGif != null)
                {
                    BackgroundGif.Source = null;
                }
            }
        }

        private void UpdateVinylAnimation()
        {
            try
            {
                if (_vinylRotationStoryboard != null)
                {
                    if (_viewModel.IsVinylRotating)
                    {
                        if (_vinylRotationStoryboard.GetCurrentState() != ClockState.Active)
                        {
                            _vinylRotationStoryboard.Begin();
                            System.Diagnostics.Debug.WriteLine("🎵 Animación del vinilo iniciada");
                        }
                    }
                    else
                    {
                        if (_vinylRotationStoryboard.GetCurrentState() == ClockState.Active)
                        {
                            _vinylRotationStoryboard.Pause();
                            System.Diagnostics.Debug.WriteLine("⏸️ Animación del vinilo pausada");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"❌ Error actualizando animación del vinilo: {ex.Message}");
            }
        }

        private void UpdateWindowTitle()
        {
            try
            {
                if (_viewModel.CurrentSong != null)
                {
                    Title = $"StunsCat Music Player - {_viewModel.CurrentSong.Artist} - {_viewModel.CurrentSong.Title}";
                }
                else
                {
                    Title = "StunsCat Music Player";
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"❌ Error actualizando título de ventana: {ex.Message}");
                Title = "StunsCat Music Player";
            }
        }

        private void UpdateLoadingState()
        {
            try
            {
                // Opcional: Mostrar indicador de carga
                if (_viewModel.IsSongLoading)
                {
                    Cursor = Cursors.Wait;
                    System.Diagnostics.Debug.WriteLine("⏳ Estado de carga: Cargando...");
                }
                else
                {
                    Cursor = Cursors.Arrow;
                    System.Diagnostics.Debug.WriteLine("✅ Estado de carga: Completado");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"❌ Error actualizando estado de carga: {ex.Message}");
                Cursor = Cursors.Arrow; // Fallback
            }
        }

        private void UpdateScanningState()
        {
            try
            {
                if (_viewModel.IsScanning)
                {
                    System.Diagnostics.Debug.WriteLine("🔍 Iniciando escaneo...");
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine("✅ Escaneo completado");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"❌ Error actualizando estado de escaneo: {ex.Message}");
            }
        }

        #endregion

        #region Eventos de Ventana

        private void MainWindow_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed && !_isMaximized)
            {
                try
                {
                    DragMove();
                }
                catch (InvalidOperationException)
                {
                    // Ignorar si la ventana no se puede mover (ej: maximizada)
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"❌ Error moviendo ventana: {ex.Message}");
                }
            }
        }

        private void MainWindow_StateChanged(object sender, EventArgs e)
        {
            try
            {
                if (WindowState == WindowState.Maximized)
                {
                    _isMaximized = true;
                }
                else if (WindowState == WindowState.Normal)
                {
                    _isMaximized = false;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"❌ Error en StateChanged: {ex.Message}");
            }
        }

        private void MainWindow_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            try
            {
                if (WindowState == WindowState.Normal && !_isMaximized)
                {
                    _previousWidth = e.NewSize.Width;
                    _previousHeight = e.NewSize.Height;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"❌ Error en SizeChanged: {ex.Message}");
            }
        }

        private void MainWindow_KeyDown(object sender, KeyEventArgs e)
        {
            if (_isDisposed || _viewModel == null)
                return;

            try
            {
                // Atajos de teclado
                switch (e.Key)
                {
                    case Key.Space:
                        if (_viewModel.TogglePlayPauseCommand?.CanExecute(null) == true)
                        {
                            _viewModel.TogglePlayPauseCommand.Execute(null);
                            e.Handled = true;
                        }
                        break;

                    case Key.Right when e.KeyboardDevice.Modifiers == ModifierKeys.Control:
                        if (_viewModel.NextSongCommand?.CanExecute(null) == true)
                        {
                            _viewModel.NextSongCommand.Execute(null);
                            e.Handled = true;
                        }
                        break;

                    case Key.Left when e.KeyboardDevice.Modifiers == ModifierKeys.Control:
                        if (_viewModel.PreviousSongCommand?.CanExecute(null) == true)
                        {
                            _viewModel.PreviousSongCommand.Execute(null);
                            e.Handled = true;
                        }
                        break;

                    case Key.S when e.KeyboardDevice.Modifiers == ModifierKeys.Control:
                        if (_viewModel.ToggleShuffleCommand?.CanExecute(null) == true)
                        {
                            _viewModel.ToggleShuffleCommand.Execute(null);
                            e.Handled = true;
                        }
                        break;

                    case Key.L when e.KeyboardDevice.Modifiers == ModifierKeys.Control:
                        if (_viewModel.ToggleLoopCommand?.CanExecute(null) == true)
                        {
                            _viewModel.ToggleLoopCommand.Execute(null);
                            e.Handled = true;
                        }
                        break;

                    case Key.O when e.KeyboardDevice.Modifiers == ModifierKeys.Control:
                        if (_viewModel.ScanFolderCommand?.CanExecute(null) == true)
                        {
                            _viewModel.ScanFolderCommand.Execute(null);
                            e.Handled = true;
                        }
                        break;

                    case Key.Escape:
                        // Salir de pantalla completa o minimizar
                        if (_isMaximized)
                        {
                            RestoreWindow();
                        }
                        break;

                    case Key.F11:
                        // Toggle pantalla completa
                        if (_isMaximized)
                        {
                            RestoreWindow();
                        }
                        else
                        {
                            MaximizeWindow();
                        }
                        e.Handled = true;
                        break;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"❌ Error manejando tecla: {ex.Message}");
            }
        }

        private void MainWindow_Closing(object sender, CancelEventArgs e)
        {
            if (_isDisposed)
                return;

            try
            {
                System.Diagnostics.Debug.WriteLine("🔄 Iniciando cierre de aplicación...");

                // Guardar configuración antes de cerrar
                _viewModel?.SaveConfiguration();

                // Limpiar recursos
                CleanupResources();

                System.Diagnostics.Debug.WriteLine("✅ Aplicación cerrada correctamente");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"❌ Error durante el cierre: {ex.Message}");
                // No cancelar el cierre por errores de limpieza
            }
        }

        #endregion

        #region Controles de Ventana

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void MinimizeButton_Click(object sender, RoutedEventArgs e)
        {
            WindowState = WindowState.Minimized;
        }

        private void MaximizeButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (_isMaximized || WindowState == WindowState.Maximized)
                {
                    RestoreWindow();
                    UpdateMaximizeButtonContent(sender as Button, "☐");
                }
                else
                {
                    MaximizeWindow();
                    UpdateMaximizeButtonContent(sender as Button, "❐");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"❌ Error maximizando/restaurando ventana: {ex.Message}");
            }
        }

        private void RestoreWindow()
        {
            try
            {
                // Restaurar ventana
                WindowState = WindowState.Normal;
                Width = _previousWidth;
                Height = _previousHeight;
                Left = _previousLeft;
                Top = _previousTop;
                _isMaximized = false;

                System.Diagnostics.Debug.WriteLine("🔄 Ventana restaurada");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"❌ Error restaurando ventana: {ex.Message}");
            }
        }

        private void MaximizeWindow()
        {
            try
            {
                // Guardar estado actual antes de maximizar
                if (WindowState == WindowState.Normal)
                {
                    _previousWidth = Width;
                    _previousHeight = Height;
                    _previousLeft = Left;
                    _previousTop = Top;
                }

                // Maximizar ventana
                WindowState = WindowState.Maximized;
                _isMaximized = true;

                System.Diagnostics.Debug.WriteLine("🔄 Ventana maximizada");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"❌ Error maximizando ventana: {ex.Message}");
            }
        }

        private void UpdateMaximizeButtonContent(Button button, string content)
        {
            try
            {
                if (button != null)
                {
                    button.Content = content;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"❌ Error actualizando botón maximizar: {ex.Message}");
            }
        }

        #endregion

        #region Control del Slider de Progreso

        private void ProgressSlider_PreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            if (_viewModel?.HasSong == true && !_viewModel.IsSongLoading)
            {
                _isUserDragging = true;
                System.Diagnostics.Debug.WriteLine("🎯 Usuario comenzó a arrastrar el slider");
            }
        }

        private void ProgressSlider_PreviewMouseUp(object sender, MouseButtonEventArgs e)
        {
            if (!_isUserDragging)
                return;

            try
            {
                _isUserDragging = false;
                System.Diagnostics.Debug.WriteLine("🎯 Usuario terminó de arrastrar el slider");

                if (sender is Slider slider && _viewModel?.HasSong == true && !_viewModel.IsSongLoading)
                {
                    var newPosition = TimeSpan.FromSeconds(slider.Value);
                    _viewModel.SeekToPosition(newPosition);
                    System.Diagnostics.Debug.WriteLine($"🎵 Buscando posición: {newPosition:mm\\:ss}");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"❌ Error buscando posición: {ex.Message}");
            }
            finally
            {
                _isUserDragging = false;
            }
        }

        private void ProgressSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            // Solo mostrar preview si el usuario está arrastrando
            if (_isUserDragging && sender is Slider)
            {
                var newPosition = TimeSpan.FromSeconds(e.NewValue);
                System.Diagnostics.Debug.WriteLine($"🎵 Preview posición: {newPosition:mm\\:ss}");
            }
        }

        #endregion

        #region Eventos Adicionales

        private void ProgressBar_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            // Manejar cambios en la barra de progreso del escaneo
            if (sender is ProgressBar progressBar)
            {
                System.Diagnostics.Debug.WriteLine($"📊 Progreso de escaneo: {progressBar.Value:F1}%");
            }
        }

        #endregion

        #region Métodos de Utilidad

        /// <summary>
        /// Centra la ventana en la pantalla
        /// </summary>
        public void CenterWindow()
        {
            try
            {
                var screenWidth = SystemParameters.PrimaryScreenWidth;
                var screenHeight = SystemParameters.PrimaryScreenHeight;

                Left = (screenWidth - Width) / 2;
                Top = (screenHeight - Height) / 2;

                System.Diagnostics.Debug.WriteLine("📐 Ventana centrada");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"❌ Error centrando ventana: {ex.Message}");
            }
        }

        /// <summary>
        /// Aplica un tema específico a la ventana
        /// </summary>
        /// <param name="themeName">Nombre del tema</param>
        public void ApplyTheme(string themeName)
        {
            try
            {
                _viewModel?.ApplyTheme(themeName);
                System.Diagnostics.Debug.WriteLine($"🎨 Tema aplicado: {themeName}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"❌ Error aplicando tema: {ex.Message}");
            }
        }

        #endregion

        #region Cleanup

        private void CleanupResources()
        {
            if (_isDisposed)
                return;

            try
            {
                _isDisposed = true;

                // Detener y limpiar timer
                if (_uiUpdateTimer != null)
                {
                    _uiUpdateTimer.Stop();
                    _uiUpdateTimer.Tick -= UIUpdateTimer_Tick;
                    _uiUpdateTimer = null;
                }

                // Detener animaciones
                if (_vinylRotationStoryboard != null)
                {
                    _vinylRotationStoryboard.Stop();
                    _vinylRotationStoryboard = null;
                }

                // Desuscribirse de eventos del ViewModel
                if (_viewModel != null)
                {
                    _viewModel.PropertyChanged -= ViewModel_PropertyChanged;
                    _viewModel.Dispose();
                    _viewModel = null;
                }

                // Limpiar eventos de ventana
                MouseDown -= MainWindow_MouseDown;
                KeyDown -= MainWindow_KeyDown;
                Closing -= MainWindow_Closing;
                StateChanged -= MainWindow_StateChanged;
                SizeChanged -= MainWindow_SizeChanged;

                System.Diagnostics.Debug.WriteLine("🧹 Recursos de MainWindow limpiados");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"❌ Error limpiando recursos: {ex.Message}");
            }
        }

        protected override void OnClosing(CancelEventArgs e)
        {
            try
            {
                if (!_isDisposed)
                {
                    CleanupResources();
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"❌ Error durante OnClosing: {ex.Message}");
                // No cancelar el cierre por errores de limpieza
            }
            finally
            {
                base.OnClosing(e);
            }
        }

        #endregion
    }
}