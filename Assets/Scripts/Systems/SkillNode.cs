// =============================================================================
// SkillNode.cs - 스킬 트리 노드 데이터 (JSON 바인딩용)
// =============================================================================
using System;

namespace BioBreach.Systems
{
    public enum SkillStatType { MoveSpeed, JumpHeight, MaxHp }

    [Serializable]
    public class SkillNode
    {
        public string id;
        public string displayName;
        public string description;
        /// <summary>JSON 문자열 — "MoveSpeed" / "JumpHeight" / "MaxHp"</summary>
        public string statType;
        public float  bonusValue;
        public string prerequisiteId;   // null/empty = 루트 노드
        public int    cost;
        public int    treeColumn;       // 0=이동속도, 1=점프력, 2=체력
        public int    treeRow;          // 0=1단계, 1=2단계, 2=3단계

        /// <summary>statType 문자열을 enum으로 변환</summary>
        public SkillStatType ParsedStatType =>
            Enum.TryParse<SkillStatType>(statType, out var v) ? v : SkillStatType.MoveSpeed;
    }
}
