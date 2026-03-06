// =============================================================================
// EnemyRepository.cs - enemies.json 데이터 저장소
// =============================================================================
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using UnityEngine;

namespace BioBreach.Engine.Data
{
    public class EnemyRepository
    {
        readonly Dictionary<string, EnemyData> _data = new();

        public IReadOnlyDictionary<string, EnemyData> All => _data;

        public void LoadFile(string fullPath)
        {
            if (!File.Exists(fullPath))
            {
                Debug.LogWarning($"[EnemyRepository] Not found: {fullPath}");
                return;
            }
            var list = JsonConvert.DeserializeObject<List<EnemyData>>(File.ReadAllText(fullPath));
            if (list == null) return;
            foreach (var d in list)
                if (!string.IsNullOrEmpty(d.id))
                    _data[d.id] = d;
            Debug.Log($"[EnemyRepository] Loaded {list.Count} entries from {Path.GetFileName(fullPath)}");
        }

        public bool TryGet(string id, out EnemyData data)
            => _data.TryGetValue(id, out data);
    }
}
