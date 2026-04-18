using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using KamPay.Models;
using KamPay.Services;
using KamPay.Services.Auth;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;

namespace KamPay.ViewModels
{
    /// <summary>
    /// ?? ARMUT MODELÝ: Müþteri hizmet talebi oluþturma ViewModel
    /// </summary>
    public partial class CreateCustomerRequestViewModel : ObservableObject
    {
        private readonly IServiceSharingService _serviceService;
        private readonly IAuthenticationService _authService;
        private readonly IStorageService _storageService;

        [ObservableProperty]
        private bool isLoading;

        [ObservableProperty]
        private bool isPosting;

        // Form alanlarý
        [ObservableProperty]
        private string title = "";

        [ObservableProperty]
        private string description = "";

        [ObservableProperty]
        private ServiceCategory selectedCategory;

        [ObservableProperty]
        private DateTime? preferredDate;

        [ObservableProperty]
        private string location = "";

        [ObservableProperty]
        private decimal? budgetMin;

        [ObservableProperty]
        private decimal? budgetMax;

        [ObservableProperty]
        private DateTime? deadline;

        [ObservableProperty]
        private string customerNotes = "";

        // Görseller
        public ObservableCollection<string> ImageUrls { get; } = new();

        // Kategoriler
        public List<ServiceCategory> Categories { get; } = Enum.GetValues(typeof(ServiceCategory))
            .Cast<ServiceCategory>()
            .ToList();

        public CreateCustomerRequestViewModel(
            IServiceSharingService serviceService,
            IAuthenticationService authService,
            IStorageService storageService)
        {
            _serviceService = serviceService;
            _authService = authService;
            _storageService = storageService;
        }

        /// <summary>
        /// Fotoðraf ekleme
        /// </summary>
        [RelayCommand]
        private async Task AddPhotoAsync()
        {
            try
            {
                if (ImageUrls.Count >= 5)
                {
                    await Shell.Current.DisplayAlert("Uyarý", "En fazla 5 fotoðraf ekleyebilirsiniz.", "Tamam");
                    return;
                }

                var photo = await MediaPicker.Default.PickPhotoAsync(new MediaPickerOptions
                {
                    Title = "Fotoðraf Seç"
                });

                if (photo == null) return;

                IsLoading = true;

                // Firebase Storage'a yükle (Geçici product ID)
                var tempProductId = Guid.NewGuid().ToString();
                var uploadResult = await _storageService.UploadProductImageAsync(
                    photo.FullPath,
                    tempProductId,
                    ImageUrls.Count
                );

                if (uploadResult.Success)
                {
                    ImageUrls.Add(uploadResult.Data);
                }
                else
                {
                    await Shell.Current.DisplayAlert("Hata", uploadResult.Message, "Tamam");
                }
            }
            catch (Exception ex)
            {
                await Shell.Current.DisplayAlert("Hata", ex.Message, "Tamam");
            }
            finally
            {
                IsLoading = false;
            }
        }

        /// <summary>
        /// Fotoðraf silme
        /// </summary>
        [RelayCommand]
        private void RemovePhoto(string imageUrl)
        {
            if (ImageUrls.Contains(imageUrl))
            {
                ImageUrls.Remove(imageUrl);
            }
        }

        /// <summary>
        /// Talep oluþturma
        /// </summary>
        [RelayCommand]
        private async Task CreateRequestAsync()
        {
            try
            {
                // Validasyon
                if (string.IsNullOrWhiteSpace(Title))
                {
                    await Shell.Current.DisplayAlert("Uyarý", "Baþlýk gerekli", "Tamam");
                    return;
                }

                if (string.IsNullOrWhiteSpace(Description))
                {
                    await Shell.Current.DisplayAlert("Uyarý", "Açýklama gerekli", "Tamam");
                    return;
                }

                if (string.IsNullOrWhiteSpace(Location))
                {
                    await Shell.Current.DisplayAlert("Uyarý", "Konum gerekli", "Tamam");
                    return;
                }

                if (BudgetMin.HasValue && BudgetMax.HasValue && BudgetMin > BudgetMax)
                {
                    await Shell.Current.DisplayAlert("Uyarý", "Minimum bütçe maksimum bütçeden büyük olamaz", "Tamam");
                    return;
                }

                var currentUser = await _authService.GetCurrentUserAsync();
                if (currentUser == null)
                {
                    await Shell.Current.DisplayAlert("Hata", "Oturum açýlmamýþ", "Tamam");
                    return;
                }

                IsPosting = true;

                // Talep oluþtur
                var request = new CustomerServiceRequest
                {
                    CustomerId = currentUser.UserId,
                    CustomerName = currentUser.FullName,
                    CustomerPhotoUrl = currentUser.ProfileImageUrl ?? "default_avatar.png",
                    Category = SelectedCategory,
                    Title = Title.Trim(),
                    Description = Description.Trim(),
                    Location = Location.Trim(),
                    PreferredDate = PreferredDate,
                    BudgetMin = BudgetMin,
                    BudgetMax = BudgetMax,
                    Deadline = Deadline,
                    CustomerNotes = CustomerNotes.Trim(),
                    ImageUrls = ImageUrls.ToList()
                };

                var result = await _serviceService.CreateCustomerRequestAsync(request);

                if (result.Success)
                {
                    await Shell.Current.DisplayAlert(
                        "Baþarýlý",
                        "Talebiniz oluþturuldu! Profesyoneller kýsa süre içinde teklif gönderecektir.",
                        "Harika!"
                    );

                    // Formu temizle
                    ClearForm();

                    // Geri dön
                    await Shell.Current.GoToAsync("..");
                }
                else
                {
                    await Shell.Current.DisplayAlert("Hata", result.Message, "Tamam");
                }
            }
            catch (Exception ex)
            {
                await Shell.Current.DisplayAlert("Hata", ex.Message, "Tamam");
            }
            finally
            {
                IsPosting = false;
            }
        }

        /// <summary>
        /// Formu temizle
        /// </summary>
        private void ClearForm()
        {
            Title = "";
            Description = "";
            Location = "";
            PreferredDate = null;
            BudgetMin = null;
            BudgetMax = null;
            Deadline = null;
            CustomerNotes = "";
            ImageUrls.Clear();
            SelectedCategory = 0;
        }

        /// <summary>
        /// Ýptal
        /// </summary>
        [RelayCommand]
        private async Task CancelAsync()
        {
            await Shell.Current.GoToAsync("..");
        }
    }
}
