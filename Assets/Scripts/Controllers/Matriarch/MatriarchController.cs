// =============================================================================
// MatriarchController.cs - 성체 컨트롤러
// Enemy 유닛들의 최종 이동 목표이자 방어 대상.
// MatriarchGrowthData 보너스 (MaxHp / Armor / Regen)를 실시간 반영한다.
// =============================================================================

using UnityEngine;
using UnityEngine.Events;
using BioBreach.Engine;
using BioBreach.Engine.Entity;

namespace BioBreach.Controller.Matriarch
{
    public class MatriarchController : EntityMonoBehaviour
    {
        [Header("성체 이벤트")]
        [Tooltip("HP가 0이 되면 호출됨 (게임 오버 처리 등 연결)")]
        public UnityEvent onMatriarchDestroyed;

        // =====================================================================
        // NGO 생명주기
        // =====================================================================

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();
            MatriarchBonusCache.OnBonusChanged += ApplyBonuses;
            ApplyBonuses();
        }

        public override void OnNetworkDespawn()
        {
            base.OnNetworkDespawn();
            MatriarchBonusCache.OnBonusChanged -= ApplyBonuses;
        }

        // =====================================================================
        // 서버 — 매 프레임 HP 자동 회복
        // =====================================================================

        void Update()
        {
            if (!IsServer || !IsAlive) return;
            float regen = MatriarchBonusCache.RegenBonus;
            if (regen > 0f) Heal(regen * Time.deltaTime);
        }

        // =====================================================================
        // 피해 감소 (방어막 적용)
        // =====================================================================

        public override void TakeDamage(float amount)
        {
            float armor   = Mathf.Clamp01(MatriarchBonusCache.ArmorBonus);
            base.TakeDamage(amount * (1f - armor));
        }

        // =====================================================================
        // 보너스 적용
        // =====================================================================

        void ApplyBonuses()
        {
            maxHpBonus = MatriarchBonusCache.MaxHpBonus;
        }

        protected override void HandleDeath()
        {
            onMatriarchDestroyed?.Invoke();
            base.HandleDeath();
        }
    }
}
