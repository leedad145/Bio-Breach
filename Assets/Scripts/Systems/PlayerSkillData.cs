// =============================================================================
// PlayerSkillData.cs - 플레이어 개인 스킬 데이터 (DontDestroyOnLoad 싱글톤)
// 씬 전환 후에도 유지되며, 스킬 잠금 해제와 포인트를 관리한다.
// 스킬 효과는 개인에게만 적용 (네트워크 비동기).
// =============================================================================
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using Newtonsoft.Json;

namespace BioBreach.Systems
{
    public class PlayerSkillData : MonoBehaviour
    {
        public static PlayerSkillData Instance { get; private set; }

        /// <summary>스킬 잠금 해제 / 포인트 변경 시 발생 (PlayerController가 구독)</summary>
        public static event System.Action OnSkillChanged;

        [Header("초기 스킬 포인트")]
        [Tooltip("게임 시작 시 지급되는 기본 포인트")]
        public int initialSkillPoints = 3;

        private int             _skillPoints;
        private HashSet<string> _unlocked = new HashSet<string>();

        public int SkillPoints => _skillPoints;

        // =====================================================================
        // 스킬 노드 — JSON 로딩 (StreamingAssets/Data/skill_nodes.json)
        // =====================================================================

        private static SkillNode[] _allNodes;

        public static SkillNode[] AllNodes => _allNodes ?? LoadNodes();

        private static SkillNode[] LoadNodes()
        {
            string path = Path.Combine(Application.streamingAssetsPath, "Data", "skill_nodes.json");
            if (!File.Exists(path))
            {
                Debug.LogWarning($"[PlayerSkillData] skill_nodes.json not found: {path}");
                return _allNodes = new SkillNode[0];
            }
            string json = File.ReadAllText(path);
            _allNodes = JsonConvert.DeserializeObject<SkillNode[]>(json) ?? new SkillNode[0];
            return _allNodes;
        }

        // =====================================================================
        // 초기화
        // =====================================================================

        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance     = this;
            _skillPoints = initialSkillPoints;
            DontDestroyOnLoad(gameObject);
        }

        /// <summary>인스턴스가 없으면 자동 생성 (씬에 미배치 시 자동 생성)</summary>
        public static PlayerSkillData EnsureExists()
        {
            if (Instance != null) return Instance;
            var go = new GameObject("[PlayerSkillData]");
            DontDestroyOnLoad(go);
            var inst = go.AddComponent<PlayerSkillData>();
            return inst;
        }

        // =====================================================================
        // 스킬 조작
        // =====================================================================

        public bool IsUnlocked(string id) => _unlocked.Contains(id);

        public bool CanUnlock(SkillNode node)
        {
            if (IsUnlocked(node.id)) return false;
            if (_skillPoints < node.cost) return false;
            if (!string.IsNullOrEmpty(node.prerequisiteId) && !IsUnlocked(node.prerequisiteId))
                return false;
            return true;
        }

        public bool TryUnlock(SkillNode node)
        {
            if (!CanUnlock(node)) return false;
            _unlocked.Add(node.id);
            _skillPoints -= node.cost;
            OnSkillChanged?.Invoke();
            return true;
        }

        /// <summary>외부(게임 이벤트 등)에서 스킬 포인트를 지급할 때 호출</summary>
        public void GainPoints(int n)
        {
            _skillPoints += n;
            OnSkillChanged?.Invoke();
        }

        // =====================================================================
        // 합산 보너스 (PlayerController.RecalculateStats에서 사용)
        // =====================================================================

        public float TotalSpeedBonus
        {
            get
            {
                float v = 0f;
                foreach (var n in AllNodes)
                    if (n.ParsedStatType == SkillStatType.MoveSpeed && IsUnlocked(n.id)) v += n.bonusValue;
                return v;
            }
        }

        public float TotalJumpBonus
        {
            get
            {
                float v = 0f;
                foreach (var n in AllNodes)
                    if (n.ParsedStatType == SkillStatType.JumpHeight && IsUnlocked(n.id)) v += n.bonusValue;
                return v;
            }
        }

        public float TotalHpBonus
        {
            get
            {
                float v = 0f;
                foreach (var n in AllNodes)
                    if (n.ParsedStatType == SkillStatType.MaxHp && IsUnlocked(n.id)) v += n.bonusValue;
                return v;
            }
        }
    }
}
