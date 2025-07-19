using System.Windows;
using StunsCat.ViewModels;

namespace StunsCat
{
    public partial class App : Application
    {
        private MainViewModel _mainViewModel;

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            // Crear instancia del ViewModel
            _mainViewModel = new MainViewModel();

            // Crear e inicializar la ventana principal
            var mainWindow = new MainWindow
            {
                DataContext = _mainViewModel
            };
            mainWindow.Show();
        }

        protected override void OnExit(ExitEventArgs e)
        {
            _mainViewModel?.Dispose();
            base.OnExit(e);
        }
    }
}
