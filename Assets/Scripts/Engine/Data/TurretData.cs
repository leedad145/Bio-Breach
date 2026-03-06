namespace BioBreach.Engine.Data
{
    public class TurretData
    {
        public string id;
        public string displayName    = "포탑";
        public float  maxHp          = 200f;
        public float  detectionRange = 20f;
        public float  attackRange    = 18f;
        public float  attackDamage   = 15f;
        public float  attackCooldown = 0.8f;
        public string targetPriority = "Nearest"; // Nearest | LowestHp | HighestPriority
    }
}
