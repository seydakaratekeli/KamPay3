using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using KamPay.Models;
using KamPay.Services;
using System.Diagnostics;

namespace KamPay.ViewModels
{
    [QueryProperty(nameof(Post), "Post")]
    public partial class EditGoodDeedPostViewModel : ObservableObject
    {
        private readonly IGoodDeedService _goodDeedService;

        [ObservableProperty]
        private GoodDeedPost post;

        [ObservableProperty]
        private string title;

        [ObservableProperty]
        private string description;

        [ObservableProperty]
        private PostType selectedType;

        [ObservableProperty]
        private bool isSaving;

        public List<PostType> PostTypes { get; } = Enum.GetValues(typeof(PostType)).Cast<PostType>().ToList();

        public EditGoodDeedPostViewModel(IGoodDeedService goodDeedService)
        {
            _goodDeedService = goodDeedService;
        }

        partial void OnPostChanged(GoodDeedPost value)
        {
            if (value != null)
            {
                Title = value.Title;
                Description = value.Description;
                SelectedType = value.Type;
            }
        }

        [RelayCommand]
        private async Task SaveAsync()
        {
            if (string.IsNullOrWhiteSpace(Title) || string.IsNullOrWhiteSpace(Description))
            {
                await Application.Current.MainPage.DisplayAlert("Uyarı", "Başlık ve açıklama boş bırakılamaz.", "Tamam");
                return;
            }

            IsSaving = true;

            try
            {
                Post.Title = Title;
                Post.Description = Description;
                Post.Type = SelectedType;

                var result = await _goodDeedService.UpdatePostAsync(Post);

                if (result.Success)
                {
                    await Application.Current.MainPage.DisplayAlert("Başarılı", "İlan başarıyla güncellendi.", "Tamam");
                    await Shell.Current.GoToAsync("..");
                }
                else
                {
                    await Application.Current.MainPage.DisplayAlert("Hata", result.Message, "Tamam");
                }
            }
            finally
            {
                IsSaving = false;
            }
        }

        [RelayCommand]
        private async Task CancelAsync()
        {
            await Shell.Current.GoToAsync("..");
        }
    }
}
