using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using KamPay.ViewModels;

namespace KamPay.Models
{// 🔥 Yeni mesaj geldiğinde scroll yapmak için kullanılan messenger
    public class ScrollToChatMessage
    {
        public Message Message { get; }

        public ScrollToChatMessage(Message message)
        {
            Message = message;
        }
    }
}