// =============================================================================
// PlayerAction.cs - 전투 / 채굴 / 아이템 디스패치
//
// 마인크래프트식 context-sensitive 액션:
//   아이템 없음      → 맨손 스탯으로 공격 + 채굴
//   MeleeWeapon 장착 → 강화된 공격 스탯 + 맨손 채굴 스탯
//   UniversalMiner   → 맨손 공격 스탯 + 강화된 채굴 스탯
//   기타 아이템      → Action1/Action2 위임 (기존 방식)
// =============================================================================
using UnityEngine;
using BioBreach.Core.Voxel;
using BioBreach.Engine.Data;
using BioBreach.Engine.Entity;
using BioBreach.Engine.Inventory;
using BioBreach.Engine.Item;
using BioBreach.Systems;

namespace BioBreach.Controller.Player
{
    public class PlayerAction : MonoBehaviour
    {
        // =====================================================================
        // Inspector 설정
        // =====================================================================

        [Header("쿨다운 / 레이캐스트")]
        public float actionCooldown   = 0.1f;
        public float interactDistance = 20f;

        [Header("설치")]
        public float placeNormalOffset = 0.05f;

        [Header("맨손 채굴 (아이템 미장착 시 기본값)")]
        public float bareHandMineRadius   = 2f;
        public float bareHandMineStrength = 0.1f;

        [Header("맨손 공격 (아이템 미장착 시 기본값)")]
        public float     bareHandAttackDamage = 5f;
        public float     bareHandAttackReach  = 2f;
        public float     bareHandAttackRadius = 1f;
        public LayerMask enemyLayer;

        [Header("복셀 드롭 매핑 (인덱스 = VoxelType 정수값)")]
        [Tooltip("0=Air(빈칸), 1=Protein, 2=Iron, 3=Calcium, 4=GeneticEssence, 5=Lipid, 6=Marrow")]
        public string[] voxelDropIds;

        // =====================================================================
        // 공개 프로퍼티 (IPlayerContext 위임용)
        // =====================================================================

        public float PlaceNormalOffset => placeNormalOffset;

        // 맨손 채굴 누적값 — PlayerHUD 에서 읽어 HUD 표시
        public readonly float[] BareHandAccumulation = new float[VoxelDatabase.TypeCount];

        // =====================================================================
        // 런타임 내부 상태
        // =====================================================================

        private PlayerInventory _inventory;
        private WorldManager    _worldManager;

        private float      _lastActionTime = -999f;
        private ItemBase[] _voxelDrops;

        private readonly Collider[] _overlapBuffer = new Collider[32];

        // =====================================================================
        // 초기화
        // =====================================================================

        public void Init(PlayerInventory inventory, WorldManager worldManager)
        {
            _inventory    = inventory;
            _worldManager = worldManager;
            GameDataLoader.EnsureLoaded();
            ResolveVoxelDrops();
        }

        public void UpdateWorldManager(WorldManager worldManager) => _worldManager = worldManager;

        void ResolveVoxelDrops()
        {
            if (voxelDropIds == null || voxelDropIds.Length == 0) return;
            _voxelDrops = new ItemBase[voxelDropIds.Length];
            for (int i = 0; i < voxelDropIds.Length; i++)
            {
                if (!string.IsNullOrEmpty(voxelDropIds[i]))
                    _voxelDrops[i] = GameDataLoader.Items.CreateItem(voxelDropIds[i]);
            }
        }

        // =====================================================================
        // 틱 — PlayerController.Update 에서 호출
        // =====================================================================

        /// <summary>
        /// 액션을 처리한다. <paramref name="ctx"/> 는 PlayerController(IPlayerContext) 를 전달한다.
        /// </summary>
        public void Tick(IPlayerContext ctx)
        {
            if (Cursor.lockState != CursorLockMode.Locked) return;
            if (Time.time - _lastActionTime < actionCooldown) return;

            var item = _inventory.SelectedItem;

            // ── 공격·채굴 이외 아이템 (설치, 소모품, 갑옷 등) ───────────────────
            if (item != null && item.data is not UniversalMiner && item.data is not MeleeWeapon)
            {
                var r1 = item.Action1(ctx);
                var r2 = item.Action2(ctx);
                if (r1.Performed || r2.Performed) _lastActionTime = Time.time;
                return;
            }

            bool performed = false;

            // ── 공격 (좌클릭 Down) ─────────────────────────────────────────────
            if (ctx.PrimaryDown)
            {
                var   w   = item?.data as MeleeWeapon;
                float dmg = w?.attackDamage ?? bareHandAttackDamage;
                float rch = w?.attackReach  ?? bareHandAttackReach;
                float rad = w?.attackRadius ?? bareHandAttackRadius;

                Vector3 center   = ctx.AttackOrigin + ctx.AttackDirection * rch;
                int     hitCount = Physics.OverlapSphereNonAlloc(center, rad, _overlapBuffer, enemyLayer);
                for (int i = 0; i < hitCount; i++)
                {
                    var entity = _overlapBuffer[i].GetComponent<EntityMonoBehaviour>();
                    if (entity != null && entity.IsAlive)
                    { entity.TakeDamage(dmg); performed = true; }
                }
            }

            // ── 채굴 (좌클릭 Hold) ─────────────────────────────────────────────
            if (ctx.PrimaryHeld && ctx.HasHit)
            {
                var   m        = item?.data as UniversalMiner;
                float radius   = m?.editRadius   ?? bareHandMineRadius;
                float strength = m?.editStrength ?? bareHandMineStrength;
                float[] acc    = m?.Accumulation ?? BareHandAccumulation;

                // 표면 법선을 따라 내부로 파고들어 실제 복셀 위치를 탐색
                Vector3   digPoint = ctx.Hit.point;
                VoxelType dug      = VoxelType.Air;
                for (int i = 1; i <= 10; i++)
                {
                    digPoint = ctx.Hit.point - ctx.Hit.normal * (i * 0.3f);
                    dug = ctx.GetVoxelTypeAt(digPoint);
                    if (dug != VoxelType.Air) break;
                }

                if (dug != VoxelType.Air)
                {
                    float[] dugAmounts = ctx.ModifyTerrain(digPoint, radius, strength, VoxelType.Air);
                    int     idx        = (int)dug;
                    float   dugAmount  = dugAmounts[idx];

                    if (dugAmount > 0f)
                    {
                        acc[idx] += dugAmount / strength;
                        float threshold = VoxelDatabase.GetDropThreshold(dug);
                        if (acc[idx] >= threshold)
                        {
                            acc[idx] -= threshold;
                            if (_voxelDrops != null && idx < _voxelDrops.Length && _voxelDrops[idx] != null)
                                _inventory.TryAddItem(_voxelDrops[idx], 1);
                        }
                        performed = true;
                    }
                }
            }

            if (performed) _lastActionTime = Time.time;
        }
    }
}
