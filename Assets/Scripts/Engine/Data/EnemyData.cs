namespace BioBreach.Engine.Data
{
    public class EnemyData
    {
        public string id;
        public string displayName       = "면역 세포";
        public float  maxHp             = 100f;
        public float  moveSpeed         = 5f;
        public float  gravityMultiplier = 2f;
        public float  detectionRange    = 15f;
        public float  attackRange       = 2f;
        public float  attackDamage      = 10f;
        public float  attackCooldown    = 1f;
        public string targetPriority    = "Nearest"; // Nearest | LowestHp | HighestPriority
        public float  digDetectDist     = 3f;
        public float  digRadius         = 3f;
        public float  digStrength       = 2f;

        // ── 타입별 특수 필드 ──────────────────────────────────────────────────
        /// <summary>Normal | Tanker | Exploder | Acid | Healer</summary>
        public string enemyType         = "Normal";
        // Tanker & Exploder
        public float  explosionRadius   = 5f;
        public float  explosionDamage   = 35f;
        // Acid
        public float  acidInterval      = 2f;
        public float  acidRadius        = 3f;
        public float  acidDigStrength   = 2f;
        // Healer
        public float  healRadius        = 8f;
        public float  healPerSecond     = 8f;
        public float  healCooldown      = 2f;
    }
}
