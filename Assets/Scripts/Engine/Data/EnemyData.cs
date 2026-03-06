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
    }
}
