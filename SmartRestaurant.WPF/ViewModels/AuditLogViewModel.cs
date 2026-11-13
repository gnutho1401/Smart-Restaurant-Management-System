using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using SmartRestaurant.Core.Entities;
using SmartRestaurant.Core.Interfaces;
using SmartRestaurant.WPF.Common;

namespace SmartRestaurant.WPF.ViewModels;

/// <summary>
/// ViewModel managing the query, filtering, and inspection of immutable audit log records.
/// Provides Non-Repudiation for restaurant managers and administrators.
/// </summary>
public class AuditLogViewModel : ViewModelBase
{
    private readonly IAuditService _auditService;
    private List<AuditLog> _allLogs = new();
    private ObservableCollection<AuditLog> _filteredLogs = new();
    private string _searchKeyword = string.Empty;
    private string _selectedActionFilter = "Tất Cả";
    private bool _isLoading;

    public ObservableCollection<AuditLog> FilteredLogs
    {
        get => _filteredLogs;
        set => SetProperty(ref _filteredLogs, value);
    }

    public string SearchKeyword
    {
        get => _searchKeyword;
        set
        {
            if (SetProperty(ref _searchKeyword, value))
            {
                ApplyFilter();
            }
        }
    }

    public string SelectedActionFilter
    {
        get => _selectedActionFilter;
        set
        {
            if (SetProperty(ref _selectedActionFilter, value))
            {
                ApplyFilter();
            }
        }
    }

    public bool IsLoading
    {
        get => _isLoading;
        set => SetProperty(ref _isLoading, value);
    }

    public int TotalLogsCount => _allLogs.Count;

    public IReadOnlyList<string> ActionFilters { get; } = new List<string>
    {
        "Tất Cả",
        "VoidOrderItem",
        "ApplyDiscount",
        "TransferTable",
        "MergeTable",
        "OpenShift",
        "CloseShift",
        "CreateStaff",
        "UpdateStaffRole",
        "DeleteStaff"
    };

    public IAsyncRelayCommand LoadLogsCommand { get; }
    public RelayCommand ClearFilterCommand { get; }

    public AuditLogViewModel(IAuditService auditService)
    {
        _auditService = auditService;
        LoadLogsCommand = new AsyncRelayCommand(LoadLogsAsync);
        ClearFilterCommand = new RelayCommand(ClearFilter);
    }

    public async Task LoadLogsAsync()
    {
        IsLoading = true;
        try
        {
            var logs = await _auditService.GetAuditLogsAsync();
            _allLogs = logs.ToList();
            OnPropertyChanged(nameof(TotalLogsCount));
            ApplyFilter();
        }
        finally
        {
            IsLoading = false;
        }
    }

    private void ApplyFilter()
    {
        var query = _allLogs.AsEnumerable();

        if (SelectedActionFilter != "Tất Cả")
        {
            query = query.Where(l => l.Action.Equals(SelectedActionFilter, StringComparison.OrdinalIgnoreCase));
        }

        if (!string.IsNullOrWhiteSpace(SearchKeyword))
        {
            string keyword = SearchKeyword.Trim();
            query = query.Where(l =>
                l.Username.Contains(keyword, StringComparison.OrdinalIgnoreCase) ||
                l.Action.Contains(keyword, StringComparison.OrdinalIgnoreCase) ||
                l.EntityName.Contains(keyword, StringComparison.OrdinalIgnoreCase) ||
                (l.Details != null && l.Details.Contains(keyword, StringComparison.OrdinalIgnoreCase)) ||
                (l.EntityId != null && l.EntityId.Contains(keyword, StringComparison.OrdinalIgnoreCase)));
        }

        FilteredLogs = new ObservableCollection<AuditLog>(query);
    }

    private void ClearFilter()
    {
        SearchKeyword = string.Empty;
        SelectedActionFilter = "Tất Cả";
        ApplyFilter();
    }
}

