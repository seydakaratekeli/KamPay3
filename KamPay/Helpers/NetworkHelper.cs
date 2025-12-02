using KamPay.Models;
using System;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;

namespace KamPay.Helpers
{
    /// <summary>
    /// Helper class for handling network operations with retry logic and error handling
    /// </summary>
    public static class NetworkHelper
    {
        /// <summary>
        /// Maximum number of retry attempts for failed requests
        /// </summary>
        public const int MaxRetryAttempts = 3;

        /// <summary>
        /// Base delay in milliseconds between retry attempts (will be multiplied by attempt number)
        /// </summary>
        public const int RetryDelayMs = 1000;

        /// <summary>
        /// Executes an async operation with retry logic
        /// </summary>
        public static async Task<T> ExecuteWithRetryAsync<T>(
            Func<Task<T>> operation,
            int maxRetries = MaxRetryAttempts,
            int delayMs = RetryDelayMs,
            Func<Exception, bool> shouldRetry = null)
        {
            int attempt = 0;
            Exception lastException = null;

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

        /// <summary>
        /// Determines if an exception is retriable based on its type and properties
        /// </summary>
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

        /// <summary>
        /// Gets a user-friendly error message for network-related exceptions
        /// </summary>
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

        /// <summary>
        /// Checks if device has internet connectivity
        /// </summary>
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

        /// <summary>
        /// Gets the current network connection type
        /// </summary>
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

        /// <summary>
        /// Executes an operation only if internet is available
        /// </summary>
        public static async Task<T> ExecuteIfOnlineAsync<T>(
            Func<Task<T>> operation,
            T offlineValue = default,
            string offlineMessage = "İnternet bağlantısı gerekli")
        {
            if (!HasInternetConnection())
            {
                throw new InvalidOperationException(offlineMessage);
            }

            return await operation();
        }

        /// <summary>
        /// Wraps an operation with network error handling
        /// </summary>
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

        /// <summary>
        /// Throttles operation execution to prevent rate limiting
        /// NOTE: Uses static fields for simplicity. In a multi-user scenario,
        /// consider using a per-operation or per-user throttling mechanism.
        /// </summary>
        private static DateTime _lastRequestTime = DateTime.MinValue;
        private static readonly object _throttleLock = new object();
        
        public static async Task ThrottleRequestAsync(int minDelayMs = 100)
        {
            lock (_throttleLock)
            {
                var timeSinceLastRequest = DateTime.UtcNow - _lastRequestTime;
                var requiredDelay = TimeSpan.FromMilliseconds(minDelayMs);
                
                if (timeSinceLastRequest < requiredDelay)
                {
                    var remainingDelay = requiredDelay - timeSinceLastRequest;
                    Task.Delay(remainingDelay).Wait();
                }
                
                _lastRequestTime = DateTime.UtcNow;
            }
        }
    }

    /// <summary>
    /// Exception thrown when an operation requires network connectivity but device is offline
    /// </summary>
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

    /// <summary>
    /// Exception thrown when rate limit is exceeded
    /// </summary>
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
