using System.Collections.Generic;
using System.Linq;

namespace KamPay.Models
{
    public class ValidationResult
    {
        public bool IsValid { get; set; }
        public List<string> Errors { get; set; }

        public ValidationResult()
        {
            Errors = new List<string>();
            IsValid = true;
        }

        public void AddError(string error)
        {
            Errors.Add(error);
            IsValid = false;
        }

        public string GetErrorMessage()
        {
            return string.Join("\n", Errors);
        }
    }

    public class ServiceResult<T>
    {
        public bool Success { get; set; }
        public T Data { get; set; }
        public string Message { get; set; }
        public List<string> Errors { get; set; }

        public ServiceResult()
        {
            Errors = new List<string>();
        }

        public static ServiceResult<T> SuccessResult(T data, string message = "Ýþlem baþarýlý")
        {
            return new ServiceResult<T>
            {
                Success = true,
                Data = data, 
                Message = message
            };
        }

        public static ServiceResult<T> FailureResult(string message, params string[] errors)
        {
            return new ServiceResult<T>
            {
                Success = false,
                Message = message,
                Errors = errors.ToList()
            };
        }
    }
}