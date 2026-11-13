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

    public MainWindow(MainViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        DataContext = _viewModel;

        // Auto-Lock Idle Monitor (3-minute inactivity timeout)
        _idleTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMinutes(3)
        };
        _idleTimer.Tick += (s, e) =>
        {
            if (!_viewModel.IsScreenLocked)
            {
                _viewModel.LockScreenCommand.Execute(null);
            }
        };

        PreviewMouseMove += (s, e) => ResetIdleTimer();
        PreviewMouseDown += (s, e) => ResetIdleTimer();
        PreviewKeyDown += (s, e) => ResetIdleTimer();
        PreviewTouchDown += (s, e) => ResetIdleTimer();

        Loaded += (s, e) => _idleTimer.Start();
        Closed += (s, e) => _idleTimer.Stop();
    }

    private void ResetIdleTimer()
    {
        if (_idleTimer.IsEnabled && !_viewModel.IsScreenLocked)
        {
            _idleTimer.Stop();
            _idleTimer.Start();
        }
    }
}
