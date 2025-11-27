
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Firebase.Database;
using Firebase.Database.Query;
using KamPay.Models;

namespace KamPay.Models
{
 
    public class SurpriseBox
    {
        public string BoxId { get; set; }
        public string DonorId { get; set; }
        public string DonorName { get; set; }
        public string ProductId { get; set; }
        public string ProductTitle { get; set; }
        public string ProductImage { get; set; }
        public DateTime CreatedAt { get; set; }
        public bool IsOpened { get; set; }
        public string? RecipientId { get; set; }
        public DateTime? OpenedAt { get; set; }

        public SurpriseBox()
        {
            BoxId = Guid.NewGuid().ToString();
            CreatedAt = DateTime.UtcNow;
            IsOpened = false;
        }
    }
}