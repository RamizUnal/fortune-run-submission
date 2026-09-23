using System;
using System.Collections.Generic;
using UnityEngine;
using Vertigo.Fortune.Domain;

namespace Vertigo.Fortune.Presentation
{
    public interface IRewardWallet { int Extractions {get;} int BestZone {get;} int GetAmount(string id); void Deposit(IReadOnlyList<RewardStack> rewards,int zone); }
    public sealed class LocalWallet : IRewardWallet
    {
        const string Key="fortune.wallet.v1";
        const string PistolPointsId = "pistol_points";
        const string LegacyPistolPointsId = "elite_pistol_points";
        [Serializable] public sealed class Entry { public string Id; public int Amount; }
        [Serializable] public sealed class Data { public int Extractions;public int BestZone;public List<Entry> Items=new List<Entry>(); }
        readonly Data data;
        public int Extractions=>data.Extractions;
        public int BestZone=>data.BestZone;
        public LocalWallet()
        {
            try { data = JsonUtility.FromJson<Data>(PlayerPrefs.GetString(Key, "")) ?? new Data(); }
            catch { data = new Data(); }
            if (data.Items == null) data.Items = new List<Entry>();
            if (MergePistolPoints(data)) Save();
        }
        public int GetAmount(string id){var e=data.Items.Find(x=>x.Id==id);return e==null?0:e.Amount;}
        public void Deposit(IReadOnlyList<RewardStack> rewards,int zone)
        {
            foreach(var stack in rewards)
            {
                var id = stack.Definition.Id == LegacyPistolPointsId ? PistolPointsId : stack.Definition.Id;
                var e = data.Items.Find(x => x.Id == id);
                if(e==null){e=new Entry{Id=id};data.Items.Add(e);}
                e.Amount=(int)Math.Min(int.MaxValue,(long)e.Amount+stack.Amount);
            }
            data.Extractions++;data.BestZone=Math.Max(data.BestZone,zone);Save();
        }

        static bool MergePistolPoints(Data wallet)
        {
            var legacy = wallet.Items.Find(entry => entry != null && entry.Id == LegacyPistolPointsId);
            if (legacy == null) return false;

            var combined = wallet.Items.Find(entry => entry != null && entry.Id == PistolPointsId) ?? legacy;
            combined.Id = PistolPointsId;
            for (var i = wallet.Items.Count - 1; i >= 0; i--)
            {
                var entry = wallet.Items[i];
                if (entry == null || ReferenceEquals(entry, combined)) continue;
                if (entry.Id != PistolPointsId && entry.Id != LegacyPistolPointsId) continue;
                combined.Amount = (int)Math.Min(int.MaxValue, (long)combined.Amount + entry.Amount);
                wallet.Items.RemoveAt(i);
            }
            return true;
        }

        void Save()
        {
            PlayerPrefs.SetString(Key, JsonUtility.ToJson(data));
            PlayerPrefs.Save();
        }
    }
}
