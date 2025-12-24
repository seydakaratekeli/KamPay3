using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using KamPay.ViewModels;

namespace KamPay.Models
{// Yeni mesaj geldiðinde scroll yapmak için kullanýlan messenger
    public class ScrollToChatMessage
    {
        public Message Message { get; }

        public ScrollToChatMessage(Message message)
        {
            Message = message;
        }
    }
}