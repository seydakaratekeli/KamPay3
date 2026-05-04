using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using KamPay.Helpers;
using KamPay.Models;
using KamPay.Services.CampusGuide;

namespace KamPay.ViewModels;

public partial class CampusGuideViewModel : ObservableObject
{
    private readonly IMicroBusinessService _microBusinessService;
    private readonly List<MicroBusiness> _allBusinesses = new();
    private bool _hasLoaded;

    [ObservableProperty] private bool isLoading;
    [ObservableProperty] private bool isRefreshing;
    [ObservableProperty] private string searchText = string.Empty;
    [ObservableProperty] private MicroBusinessCategory? selectedCategory;
    [ObservableProperty] private string statusMessage = string.Empty;

    public ObservableRangeCollection<MicroBusiness> Businesses { get; } = new();
    public ObservableRangeCollection<MicroBusiness> FilteredBusinesses { get; } = new();

    public List<MicroBusinessCategory?> Categories { get; } =
        new List<MicroBusinessCategory?> { null }
            .Concat(Enum.GetValues(typeof(MicroBusinessCategory)).Cast<MicroBusinessCategory?>())
            .ToList();

    public bool HasBusinesses => FilteredBusinesses.Count > 0;
    public bool HasStatusMessage => !string.IsNullOrWhiteSpace(StatusMessage);

    public CampusGuideViewModel(IMicroBusinessService microBusinessService)
    {
        _microBusinessService = microBusinessService;
        FilteredBusinesses.CollectionChanged += (_, _) => OnPropertyChanged(nameof(HasBusinesses));
    }

    [RelayCommand]
    public async Task LoadAsync()
    {
        if (_hasLoaded)
            return;

        await LoadBusinessesAsync(forceRefresh: false);
        _hasLoaded = true;
    }

    [RelayCommand]
    private async Task RefreshAsync()
    {
        IsRefreshing = true;
        try
        {
            await LoadBusinessesAsync(forceRefresh: true);
        }
        finally
        {
            IsRefreshing = false;
        }
    }

    [RelayCommand]
    private async Task NavigateToBusinessDetailAsync(MicroBusiness business)
    {
        if (business == null)
            return;

        await Shell.Current.GoToAsync(nameof(KamPay.Views.CampusGuide.BusinessDetailPage), new Dictionary<string, object>
        {
            { "Business", business }
        });
    }

    [RelayCommand]
    private void ClearFilters()
    {
        SearchText = string.Empty;
        SelectedCategory = null;
        ApplyFilters();
    }

    partial void OnSearchTextChanged(string value) => ApplyFilters();
    partial void OnSelectedCategoryChanged(MicroBusinessCategory? value) => ApplyFilters();
    partial void OnStatusMessageChanged(string value) => OnPropertyChanged(nameof(HasStatusMessage));

    private async Task LoadBusinessesAsync(bool forceRefresh)
    {
        IsLoading = !IsRefreshing;
        StatusMessage = string.Empty;

        try
        {
            var result = await _microBusinessService.GetVerifiedBusinessesAsync(forceRefresh);
            if (!result.Success)
            {
                StatusMessage = result.Message;
                return;
            }

            _allBusinesses.Clear();
            _allBusinesses.AddRange(result.Data ?? new List<MicroBusiness>());
            Businesses.ReplaceRange(_allBusinesses);
            StatusMessage = result.Message == "İşlem başarılı" ? string.Empty : result.Message;
            ApplyFilters();
        }
        finally
        {
            IsLoading = false;
        }
    }

    private void ApplyFilters()
    {
        var query = _allBusinesses.AsEnumerable();

        if (!string.IsNullOrWhiteSpace(SearchText))
        {
            var search = SearchText.Trim();
            query = query.Where(b =>
                b.Name.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                b.Description.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                b.Location.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                b.DisplayCategoryName.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                b.Tags.Any(tag => tag.Contains(search, StringComparison.OrdinalIgnoreCase)));
        }

        if (SelectedCategory.HasValue)
            query = query.Where(b => b.Category == SelectedCategory.Value);

        FilteredBusinesses.ReplaceRange(query
            .OrderBy(b => b.DisplayOrder)
            .ThenBy(b => b.Name)
            .ToList());

        if (_allBusinesses.Count == 0)
        {
            StatusMessage = "Henüz doğrulanmış kampüs işletmesi bulunmuyor.";
        }
        else if (FilteredBusinesses.Count == 0)
        {
            StatusMessage = "Aramana uygun işletme bulunamadı.";
        }
        else if (StatusMessage == "Henüz doğrulanmış kampüs işletmesi bulunmuyor." ||
                 StatusMessage == "Aramana uygun işletme bulunamadı.")
        {
            StatusMessage = string.Empty;
        }
    }
}
