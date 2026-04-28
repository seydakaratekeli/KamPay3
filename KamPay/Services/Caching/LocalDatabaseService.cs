using LiteDB;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace KamPay.Services.Caching
{
    public interface ILocalDatabaseService<T> where T : class
    {
        void InsertOrUpdate(T item);
        void InsertOrUpdateBulk(IEnumerable<T> items);
        List<T> GetAll();
        void DeleteAll();
        T? GetById(string id);
        void Delete(string id);
    }

    public class LocalDatabaseService<T> : ILocalDatabaseService<T> where T : class
    {
        private readonly string _dbPath;
        private readonly string _collectionName;

        // ✅ DI FIX: string parametresi kaldırıldı, koleksiyon adı otomatik belirleniyor
        public LocalDatabaseService()
        {
            // typeof(T).Name → "ServiceOffer", "Product", vb.
            _collectionName = typeof(T).Name.ToLowerInvariant();
            
            // MAUI için güvenli yerel veritabanı yolu
            var docsPath = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            _dbPath = Path.Combine(docsPath, "KamPayLocal.db");
        }

        public void InsertOrUpdate(T item)
        {
            try
            {
                using var db = new LiteDatabase(_dbPath);
                var col = db.GetCollection<T>(_collectionName);
                col.Upsert(item);
            }
            catch (Exception ex)
            {
                KamPay.Helpers.AppLogger.DebugLog($"LiteDB Upsert Error: {ex.Message}");
            }
        }

        public void InsertOrUpdateBulk(IEnumerable<T> items)
        {
            try
            {
                if (items == null || !items.Any()) return;

                using var db = new LiteDatabase(_dbPath);
                var col = db.GetCollection<T>(_collectionName);
                col.Upsert(items);
            }
            catch (Exception ex)
            {
                KamPay.Helpers.AppLogger.DebugLog($"LiteDB Bulk Upsert Error: {ex.Message}");
            }
        }

        public List<T> GetAll()
        {
            try
            {
                using var db = new LiteDatabase(_dbPath);
                var col = db.GetCollection<T>(_collectionName);
                return col.FindAll().ToList();
            }
            catch (Exception ex)
            {
                KamPay.Helpers.AppLogger.DebugLog($"LiteDB GetAll Error: {ex.Message}");
                return new List<T>();
            }
        }

        public void DeleteAll()
        {
            try
            {
                using var db = new LiteDatabase(_dbPath);
                db.DropCollection(_collectionName);
            }
            catch (Exception ex)
            {
                KamPay.Helpers.AppLogger.DebugLog($"LiteDB DeleteAll Error: {ex.Message}");
            }
        }

        public T? GetById(string id)
        {
            try
            {
                using var db = new LiteDatabase(_dbPath);
                var col = db.GetCollection<T>(_collectionName);
                return col.FindById(new BsonValue(id));
            }
            catch (Exception ex)
            {
                KamPay.Helpers.AppLogger.DebugLog($"LiteDB GetById Error: {ex.Message}");
                return null;
            }
        }

        public void Delete(string id)
        {
            try
            {
                using var db = new LiteDatabase(_dbPath);
                var col = db.GetCollection<T>(_collectionName);
                col.Delete(new BsonValue(id));
            }
            catch (Exception ex)
            {
                KamPay.Helpers.AppLogger.DebugLog($"LiteDB Delete Error: {ex.Message}");
            }
        }
    }
}
