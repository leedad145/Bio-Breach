// =============================================================================
// TurretRepository.cs - turrets.json 데이터 저장소
// =============================================================================
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using UnityEngine;

namespace BioBreach.Engine.Data
{
    public class TurretRepository
    {
        readonly Dictionary<string, TurretData> _data = new();

        public IReadOnlyDictionary<string, TurretData> All => _data;

        public void LoadFile(string fullPath)
        {
            if (!File.Exists(fullPath))
            {
                Debug.LogWarning($"[TurretRepository] Not found: {fullPath}");
                return;
            }
            var list = JsonConvert.DeserializeObject<List<TurretData>>(File.ReadAllText(fullPath));
            if (list == null) return;
            foreach (var d in list)
                if (!string.IsNullOrEmpty(d.id))
                    _data[d.id] = d;
            Debug.Log($"[TurretRepository] Loaded {list.Count} entries from {Path.GetFileName(fullPath)}");
        }

        public bool TryGet(string id, out TurretData data)
            => _data.TryGetValue(id, out data);
    }
}
