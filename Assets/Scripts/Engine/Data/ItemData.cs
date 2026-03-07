namespace BioBreach.Engine.Data
{
    /// <summary>
    /// JSON에서 읽는 아이템 데이터. type 필드로 SO 서브클래스를 결정한다.
    /// type: "VoxelBlock" | "Usable" | "Placeable" | "MeleeWeapon" | "Equippable"
    /// </summary>
    public class ItemData
    {
        // ── 공통 ─────────────────────────────────────────────────────────────
        public string id;
        public string type;
        public string itemName    = "아이템";
        public string description = "";
        public int    gridWidth   = 1;
        public int    gridHeight  = 1;
        public int    maxStack    = 99;

        // ── VoxelBlock ────────────────────────────────────────────────────────
        public string voxelType    = "Protein"; // VoxelType enum 이름
        public string editMode     = "Both";    // Add | Remove | Both
        public float  editRadius   = 3f;
        public float  editStrength = 0.3f;

        // ── Usable ────────────────────────────────────────────────────────────
        public string effect         = "None";  // None | Heal | SpeedBoost | JumpBoost
        public float  effectValue    = 0f;
        public float  effectDuration = 0f;

        // ── Placeable ─────────────────────────────────────────────────────────
        public float placeDistance = 10f;

        // ── MeleeWeapon ───────────────────────────────────────────────────────
        public float meleeAttackDamage = 25f;
        public float meleeAttackReach  = 3f;
        public float meleeAttackRadius = 1.5f;

        // ── UniversalMiner ────────────────────────────────────────────────────
        // 인덱스 = (int)VoxelType. 0=Air(빈 문자열), 1=Protein, 2=Iron, 3=Calcium, 4=GeneticEssence
        public string[] voxelDropIds;

        // ── Equippable ────────────────────────────────────────────────────────
        // equipSlot: "Head" | "Chest" | "Hands" | "Legs" | "Feet"
        public string equipSlot         = "Chest";
        public float  hpBonus           = 0f;
        public float  moveSpeedBonus    = 0f;
        public float  jumpHeightBonus   = 0f;
        public float  attackDamageBonus = 0f;
    }
}
