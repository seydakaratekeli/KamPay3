using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Firebase.Database;
using Firebase.Database.Query;
using KamPay.Helpers;
using KamPay.Models;

namespace KamPay.Services
{
    /// <summary>
    /// ✅ SOLID İYİLEŞTİRME: IProductService artık tüm alt interface'leri implement ediyor
    /// - ISP (Interface Segregation): Küçük, odaklanmış interface'ler
    /// - LSP (Liskov Substitution): Herhangi bir alt interface yerine kullanılabilir
    /// - DIP (Dependency Inversion): Concrete class'lara değil interface'lere bağımlı
    /// </summary>
    public interface IProductService : IProductQueryService, IProductCommandService, IProductValidationService
    {
        // ✅ Tüm metodlar alt interface'lerden geliyor
        // Bu interface sadece "marker" görevi görüyor - backward compatibility için
    }
}