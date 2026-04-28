using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using KamPay.Models;
using KamPay.Services;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Maui.Controls;

namespace KamPay.ViewModels
{
    [QueryProperty(nameof(OfferId), "offerId")]
    public partial class EditServiceOfferViewModel : ObservableObject
    {
        private readonly IServiceSharingService _serviceService;

        [ObservableProperty]
        private string offerId = "";

        [ObservableProperty]
        private ServiceOffer? offer;

        [ObservableProperty]
        private bool isLoading;

        [ObservableProperty]
        private bool isUpdating;

        public ObservableCollection<ServiceCategory> Categories { get; }

        [ObservableProperty]
        private ServiceCategory selectedCategory;

        public EditServiceOfferViewModel(IServiceSharingService serviceService)
        {
            _serviceService = serviceService;
            Categories = new ObservableCollection<ServiceCategory>(Enum.GetValues(typeof(ServiceCategory)).Cast<ServiceCategory>());
        }

        partial void OnOfferIdChanged(string value)
        {
            if (!string.IsNullOrEmpty(value))
            {
                _ = LoadOfferAsync();
            }
        }

        private async Task LoadOfferAsync()
        {
            try
            {
                IsLoading = true;
                var res = await _serviceService.GetServiceOfferByIdAsync(OfferId);
                if (res.Success && res.Data != null)
                {
                    Offer = res.Data;
                    SelectedCategory = Offer.Category;
                }
                else
                {
                    await Shell.Current.DisplayAlert("Hata", "İlan bulunamadı.", "Tamam");
                    await Shell.Current.GoToAsync("..");
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

        [RelayCommand]
        private async Task UpdateOfferAsync()
        {
            if (Offer == null || IsUpdating) return;

            if (string.IsNullOrWhiteSpace(Offer.Title) || string.IsNullOrWhiteSpace(Offer.Description) || Offer.Price <= 0)
            {
                await Shell.Current.DisplayAlert("Hata", "Tüm alanları geçerli şekilde doldurun.", "Tamam");
                return;
            }

            try
            {
                IsUpdating = true;
                Offer.Category = SelectedCategory;
                var res = await _serviceService.UpdateServiceOfferAsync(Offer);
                if (res.Success)
                {
                    await Shell.Current.DisplayAlert("Başarılı", "İlan güncellendi.", "Tamam");
                    await Shell.Current.GoToAsync("..");
                }
                else
                {
                    await Shell.Current.DisplayAlert("Hata", res.Message ?? "Güncellenemedi.", "Tamam");
                }
            }
            catch (Exception ex)
            {
                await Shell.Current.DisplayAlert("Hata", ex.Message, "Tamam");
            }
            finally
            {
                IsUpdating = false;
            }
        }

        [RelayCommand]
        private void IncrementTimeCredits()
        {
            if (Offer != null)
            {
                Offer.TimeCredits++;
                OnPropertyChanged(nameof(Offer));
            }
        }

        [RelayCommand]
        private void DecrementTimeCredits()
        {
            if (Offer != null && Offer.TimeCredits > 1)
            {
                Offer.TimeCredits--;
                OnPropertyChanged(nameof(Offer));
            }
        }

        [RelayCommand]
        private async Task GoBackAsync()
        {
            await Shell.Current.GoToAsync("..");
        }
    }
}
