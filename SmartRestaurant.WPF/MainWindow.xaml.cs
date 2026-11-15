using System;
using System.Windows;
using System.Windows.Threading;
using SmartRestaurant.WPF.ViewModels;

namespace SmartRestaurant.WPF;

/// <summary>
/// Interaction logic for MainWindow.xaml
/// Incorporates SEC-02 Auto-Lock Idle Monitor protecting terminals from unauthorized walk-up access.
/// </summary>
public partial class MainWindow : Window
{
    private readonly MainViewModel _viewModel;
    private readonly DispatcherTimer _idleTimer;
    private DateTime _lastActivityTime = DateTime.UtcNow;

    public MainWindow(MainViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        DataContext = _viewModel;

        // Auto-Lock Idle Monitor (Checks every 10 seconds without stopping/starting timer on every mouse move)
        _idleTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(10)
        };
        _idleTimer.Tick += (s, e) =>
        {
            if (!_viewModel.IsScreenLocked && DateTime.UtcNow - _lastActivityTime >= TimeSpan.FromMinutes(3))
            {
                _viewModel.LockScreenCommand.Execute(null);
            }
        };

        PreviewMouseMove += (s, e) => RecordActivity();
        PreviewMouseDown += (s, e) => RecordActivity();
        PreviewKeyDown += (s, e) => RecordActivity();
        PreviewTouchDown += (s, e) => RecordActivity();

        _viewModel.PropertyChanged += (s, e) =>
        {
            if (e.PropertyName == nameof(MainViewModel.IsScreenLocked) && _viewModel.IsScreenLocked)
            {
                Dispatcher.BeginInvoke(DispatcherPriority.Input, new Action(() =>
                {
                    LockPasswordBox?.Focus();
                }));
            }
        };

        Loaded += (s, e) => _idleTimer.Start();
        Closed += (s, e) => _idleTimer.Stop();
    }

    private void RecordActivity()
    {
        _lastActivityTime = DateTime.UtcNow;
    }
}
