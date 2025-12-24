using KamPay.Models;
using System;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;

namespace KamPay.Helpers
{
    // Yeniden deneme mantığı ve hata işleme ile ağ işlemlerini yönetmek için yardımcı sınıf
    public static class NetworkHelper
    {
        // Başarısız istekler için maksimum yeniden deneme sayısı
        public const int MaxRetryAttempts = 3;

        // Yeniden deneme girişimleri arasındaki milisaniye cinsinden temel gecikme (deneme sayısı ile çarpılacaktır)
        public const int RetryDelayMs = 1000;

        // Yeniden deneme mantığıyla eşzamansız bir işlemi yürütür
        public static async Task<T> ExecuteWithRetryAsync<T>(
            Func<Task<T>> operation,
            int maxRetries = MaxRetryAttempts,
            int delayMs = RetryDelayMs,
            Func<Exception, bool>? shouldRetry = null)
        {
            int attempt = 0;
            Exception? lastException = null;

            while (attempt < maxRetries)
            {
                try
                {
                    attempt++;
                    return await operation();
                }
                catch (Exception ex)
                {
                    lastException = ex;

                    // Check if we should retry this exception
                    bool retry = shouldRetry?.Invoke(ex) ?? IsRetriableException(ex);

                    if (!retry || attempt >= maxRetries)
                    {
                        throw;
                    }

                    // Calculate delay with exponential backoff
                    int delay = delayMs * attempt;
                    await Task.Delay(delay);
                }
            }

            throw lastException ?? new Exception("Operation failed after maximum retries");
        }

        // Bir istisnanın türüne ve özelliklerine bağlı olarak yeniden denenebilir olup olmadığını belirler
        public static bool IsRetriableException(Exception ex)
        {
            // Network-related exceptions that should be retried
            if (ex is HttpRequestException)
                return true;

            if (ex is TaskCanceledException)
                return true;

            if (ex is TimeoutException)
                return true;

            // Check for specific HTTP status codes
            if (ex is WebException webEx)
            {
                var response = webEx.Response as HttpWebResponse;
                if (response != null)
                {
                    // Retry on server errors (5xx) and some client errors
                    return response.StatusCode >= HttpStatusCode.InternalServerError ||
                           response.StatusCode == HttpStatusCode.RequestTimeout ||
                           response.StatusCode == HttpStatusCode.TooManyRequests;
                }
                return true;
            }

            return false;
        }

        // Ağ ile ilgili istisnalar için kullanıcı dostu bir hata mesajı alır
        public static string GetUserFriendlyErrorMessage(Exception ex)
        {
            return ex switch
            {
                HttpRequestException => "İnternet bağlantısı hatası. Lütfen bağlantınızı kontrol edin.",
                TaskCanceledException => "İşlem zaman aşımına uğradı. Lütfen tekrar deneyin.",
                TimeoutException => "İşlem zaman aşımına uğradı. Lütfen tekrar deneyin.",
                WebException webEx when webEx.Status == WebExceptionStatus.NameResolutionFailure =>
                    "Sunucuya bağlanılamadı. İnternet bağlantınızı kontrol edin.",
                WebException webEx when webEx.Status == WebExceptionStatus.ConnectFailure =>
                    "Sunucuya bağlanılamadı. Lütfen daha sonra tekrar deneyin.",
                WebException webEx when webEx.Status == WebExceptionStatus.Timeout =>
                    "Bağlantı zaman aşımına uğradı. Lütfen tekrar deneyin.",
                _ => $"Bir hata oluştu: {ex.Message}"
            };
        }

        // Cihazın internet bağlantısı olup olmadığını kontrol eder
        public static bool HasInternetConnection()
        {
            try
            {
                var current = Microsoft.Maui.Networking.Connectivity.NetworkAccess;
                return current == Microsoft.Maui.Networking.NetworkAccess.Internet;
            }
            catch
            {
                return false;
            }
        }

        // Geçerli ağ bağlantı türünü alır
        public static string GetConnectionType()
        {
            try
            {
                var profiles = Microsoft.Maui.Networking.Connectivity.ConnectionProfiles;
                
                if (profiles.Contains(Microsoft.Maui.Networking.ConnectionProfile.WiFi))
                    return "WiFi";
                
                if (profiles.Contains(Microsoft.Maui.Networking.ConnectionProfile.Cellular))
                    return "Cellular";
                
                if (profiles.Contains(Microsoft.Maui.Networking.ConnectionProfile.Ethernet))
                    return "Ethernet";
                
                return "Unknown";
            }
            catch
            {
                return "Unknown";
            }
        }

        // Yalnızca internet bağlantısı mevcutsa bir işlemi yürütür
        public static async Task<T?> ExecuteIfOnlineAsync<T>(
            Func<Task<T>> operation,
            T? offlineValue = default,
            string offlineMessage = "İnternet bağlantısı gerekli")
        {
            if (!HasInternetConnection())
            {
                throw new InvalidOperationException(offlineMessage);
            }

            return await operation();
        }

        // Ağ hatası işleme içeren bir işlemi sarmalar
        public static async Task<ServiceResult<T>> ExecuteNetworkOperationAsync<T>(
            Func<Task<T>> operation,
            string errorMessage = "İşlem başarısız oldu")
        {
            try
            {
                if (!HasInternetConnection())
                {
                    return ServiceResult<T>.FailureResult(
                        "İnternet bağlantısı yok",
                        "Lütfen internet bağlantınızı kontrol edin ve tekrar deneyin"
                    );
                }

                var result = await ExecuteWithRetryAsync(operation);
                return ServiceResult<T>.SuccessResult(result);
            }
            catch (Exception ex)
            {
                var userMessage = GetUserFriendlyErrorMessage(ex);
                return ServiceResult<T>.FailureResult(errorMessage, userMessage);
            }
        }

        // Hız sınırlamasını önlemek için işlem yürütmesini kısıtlar

        // NOT: Basitlik için statik alanlar kullanılmıştır. Çok kullanıcılı bir senaryoda,

        // işlem başına veya kullanıcı başına kısıtlama mekanizması kullanmayı düşünün.
        private static DateTime _lastRequestTime = DateTime.MinValue;
        private static readonly object _throttleLock = new object();
        
        public static async Task ThrottleRequestAsync(int minDelayMs = 100)
        {
            TimeSpan remainingDelay;
            
            lock (_throttleLock)
            {
                var timeSinceLastRequest = DateTime.UtcNow - _lastRequestTime;
                var requiredDelay = TimeSpan.FromMilliseconds(minDelayMs);
                
                if (timeSinceLastRequest < requiredDelay)
                {
                    remainingDelay = requiredDelay - timeSinceLastRequest;
                }
                else
                {
                    remainingDelay = TimeSpan.Zero;
                    _lastRequestTime = DateTime.UtcNow;
                    return;
                }
            }
            
            // Await the delay outside the lock
            await Task.Delay(remainingDelay);
            
            lock (_throttleLock)
            {
                _lastRequestTime = DateTime.UtcNow;
            }
        }
    }

    // Bir işlem ağ bağlantısı gerektirdiğinde ancak cihaz çevrimdışı olduğunda fırlatılan istisna
    public class NoInternetException : Exception
    {
        public NoInternetException() 
            : base("İnternet bağlantısı yok")
        {
        }

        public NoInternetException(string message) 
            : base(message)
        {
        }

        public NoInternetException(string message, Exception innerException) 
            : base(message, innerException)
        {
        }
    }

    // Oran sınırı aşıldığında fırlatılan istisna
    public class RateLimitExceededException : Exception
    {
        public DateTime RetryAfter { get; set; }

        public RateLimitExceededException(DateTime retryAfter) 
            : base("İşlem limiti aşıldı. Lütfen daha sonra tekrar deneyin.")
        {
            RetryAfter = retryAfter;
        }

        public RateLimitExceededException(string message, DateTime retryAfter) 
            : base(message)
        {
            RetryAfter = retryAfter;
        }
    }
}
