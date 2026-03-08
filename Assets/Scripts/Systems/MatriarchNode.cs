// =============================================================================
// MatriarchNode.cs - 성체 성장 트리 노드 데이터 (JSON 바인딩용)
// =============================================================================
using System;

namespace BioBreach.Systems
{
    public enum MatriarchStatType
    {
        MaxHp,       // 최대 HP 증가
        Regen,       // 초당 자동 회복
        Armor,       // 받는 피해 감소 비율 (0~1)
        SpawnDelay,  // 웨이브 간격 추가 (초)
        WaveReward,  // 웨이브 클리어 보상 GE 추가량
    }

    [Serializable]
    public class MatriarchNode
    {
        public string   id;
        public string   displayName;
        public string   description;
        public string   statType;                              // JSON 문자열
        public float    bonusValue;
        public string[] prerequisiteIds = new string[0];      // 모두 해제돼야 해제 가능
        public int      cost;                                  // raw_genetic_essence 비용
        public int      treeColumn;
        public int      treeRow;

        public MatriarchStatType ParsedStatType =>
            Enum.TryParse<MatriarchStatType>(statType, out var v) ? v : MatriarchStatType.MaxHp;
    }
}
