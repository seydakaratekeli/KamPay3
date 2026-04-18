using Firebase.Database;
using Firebase.Database.Query;
using KamPay.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using KamPay.Helpers;

namespace KamPay.Services.ServiceSharing
{
    public class ServiceOfferService
    {
        private readonly FirebaseClient _firebaseClient;

        public ServiceOfferService(FirebaseClient firebaseClient)
        {
            _firebaseClient = firebaseClient ?? throw new ArgumentNullException(nameof(firebaseClient));
        }

        public async Task<ServiceResult<ServiceOffer>> CreateServiceOfferAsync(ServiceOffer offer)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(offer.ServiceId))
                    offer.ServiceId = Guid.NewGuid().ToString();

                await _firebaseClient
                    .Child(Constants.ServiceOffersCollection)
                    .Child(offer.ServiceId)
                    .PutAsync(offer);

                return ServiceResult<ServiceOffer>.SuccessResult(offer, "Hizmet paylaşıldı!");
            }
            catch (Exception ex)
            {
                return ServiceResult<ServiceOffer>.FailureResult("Hata", ex.Message);
            }
        }

        public async Task<ServiceResult<List<ServiceOffer>>> GetServiceOffersAsync(ServiceCategory? category = null)
        {
            try
            {
                var allOffers = await _firebaseClient
                    .Child(Constants.ServiceOffersCollection)
                    .OnceAsync<ServiceOffer>();

                var offers = allOffers
                    .Select(o => o.Object)
                    .Where(o => o.IsAvailable && (!category.HasValue || o.Category == category.Value))
                    .OrderByDescending(o => o.CreatedAt)
                    .ToList();

                return ServiceResult<List<ServiceOffer>>.SuccessResult(offers);
            }
            catch (Exception ex)
            {
                return ServiceResult<List<ServiceOffer>>.FailureResult("Hata", ex.Message);
            }
        }

        public async Task<ServiceResult<List<ServiceOffer>>> GetServiceOffersPagedAsync(
            int pageSize = 20,
            string? lastKey = null,
            ServiceCategory? category = null)
        {
            try
            {
                IEnumerable<Firebase.Database.FirebaseObject<ServiceOffer>> items;

                if (category.HasValue)
                {
                    items = await _firebaseClient
                        .Child(Constants.ServiceOffersCollection)
                        .OrderBy("Category")
                        .EqualTo((int)category.Value)
                        .OnceAsync<ServiceOffer>();

                    var allOffers = items
                        .Select(o =>
                        {
                            var offer = o.Object;
                            offer.ServiceId = o.Key;
                            return offer;
                        })
                        .Where(o => o.IsAvailable)
                        .OrderByDescending(o => o.CreatedAt)
                        .ToList();

                    if (!string.IsNullOrEmpty(lastKey))
                    {
                        var lastIndex = allOffers.FindIndex(o => o.ServiceId == lastKey);
                        if (lastIndex >= 0)
                        {
                            allOffers = allOffers.Skip(lastIndex + 1).Take(pageSize).ToList();
                        }
                    }
                    else
                    {
                        allOffers = allOffers.Take(pageSize).ToList();
                    }

                    return ServiceResult<List<ServiceOffer>>.SuccessResult(allOffers);
                }
                else
                {
                    // BUG-12 FİX: ".StartAt(lastKey)" kullanımı string hatası veriyordu.
                    // Çünkü "CreatedAt" timestamp (long) ile orderBy yapılırken string "ServiceId" verilemez.
                    // İstemci tarafında güvenli sayfalama kullanılarak sorun kalıcı olarak çözüldü.
                    items = await _firebaseClient
                        .Child(Constants.ServiceOffersCollection)
                        .OnceAsync<ServiceOffer>();

                    var allOffers = items
                        .Select(o =>
                        {
                            var offer = o.Object;
                            offer.ServiceId = o.Key;
                            return offer;
                        })
                        .Where(o => o.IsAvailable)
                        .OrderByDescending(o => o.CreatedAt)
                        .ToList();

                    if (!string.IsNullOrEmpty(lastKey))
                    {
                        var lastIndex = allOffers.FindIndex(o => o.ServiceId == lastKey);
                        if (lastIndex >= 0)
                        {
                            allOffers = allOffers.Skip(lastIndex + 1).Take(pageSize).ToList();
                        }
                    }
                    else
                    {
                        allOffers = allOffers.Take(pageSize).ToList();
                    }

                    return ServiceResult<List<ServiceOffer>>.SuccessResult(allOffers);
                }
            }
            catch (Exception ex)
            {
                return ServiceResult<List<ServiceOffer>>.FailureResult("Hizmetler yüklenemedi", ex.Message);
            }
        }

        public async Task<ServiceResult<bool>> UpdateUserInfoInServicesAsync(string userId, string? newName, string? newPhotoUrl)
        {
            try
            {
                var allServices = await _firebaseClient
                    .Child(Constants.ServiceOffersCollection)
                    .OrderBy("ProviderId")
                    .EqualTo(userId)
                    .OnceAsync<ServiceOffer>();

                if (!allServices.Any())
                {
                    return ServiceResult<bool>.SuccessResult(true, "Güncellenecek hizmet yok");
                }

                var patchTasks = new List<Task>();

                foreach (var serviceEntry in allServices)
                {
                    var perServiceUpdates = new Dictionary<string, object>();
                    if (!string.IsNullOrWhiteSpace(newName))
                        perServiceUpdates["ProviderName"] = newName;
                    if (!string.IsNullOrWhiteSpace(newPhotoUrl))
                        perServiceUpdates["ProviderPhotoUrl"] = newPhotoUrl;

                    if (perServiceUpdates.Any())
                    {
                        var task = _firebaseClient
                            .Child(Constants.ServiceOffersCollection)
                            .Child(serviceEntry.Key)
                            .PatchAsync(perServiceUpdates);

                        patchTasks.Add(task);
                    }
                }

                if (patchTasks.Any())
                    await Task.WhenAll(patchTasks);

                return ServiceResult<bool>.SuccessResult(true, $"{allServices.Count()} hizmet güncellendi");
            }
            catch (Exception ex)
            {
                return ServiceResult<bool>.FailureResult("Hizmetler güncellenemedi", ex.Message);
            }
        }
    }
}
