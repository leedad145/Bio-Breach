// =============================================================================
// EntityMonoBehaviour.cs - EntityBase를 Unity MonoBehaviour와 연결하는 브리지
// Engine 레이어 — UnityEngine 의존
// =============================================================================

using System;
using UnityEngine;
using BioBreach.Core.Entity;

namespace BioBreach.Engine.Entity
{
    /// <summary>
    /// 타겟 선택 우선순위.
    /// EnemyController(방어 유닛 타겟)·TurretController(적 타겟) 공용.
    /// </summary>
    public enum TargetPriority
    {
        Nearest,        // 가장 가까운 대상
        LowestHp,       // HP가 가장 낮은 대상
        HighestPriority // priorityScore가 가장 높은 대상
    }

    /// <summary>
    /// EntityBase의 HP/사망 시스템을 Unity MonoBehaviour로 래핑한 추상 기반 클래스.
    /// Enemy, Matriarch, Turret 등 모든 게임 엔티티 컴포넌트의 공통 기반.
    ///
    /// ※ 초기화는 Start()에서 이루어지므로 Instantiate 직후~Start 호출 전 사이에
    ///   maxHp 등 스탯 필드를 변경하면 반영됩니다 (EnemySpawner의 스케일링에 활용).
    /// </summary>
    public abstract class EntityMonoBehaviour : MonoBehaviour
    {
        [Header("엔티티 기본 스탯")]
        [SerializeField] string entityDisplayName = "Entity";
        [SerializeField] protected float maxHp = 100f;

        [Header("타겟 우선순위 점수")]
        [Tooltip("높을수록 적에게 우선 공격 받음 (HighestPriority 모드에서 사용)")]
        public int priorityScore = 0;

        RuntimeEntity _entity;

        public string EntityName => _entity.DisplayName;
        public float  CurrentHp  => _entity.CurrentHp;
        public float  MaxHp      => _entity.MaxHp;
        public bool   IsAlive    => _entity.IsAlive;

        /// <summary>HP가 0이 될 때 발생 (HandleDeath 호출 전)</summary>
        public event Action OnDeathEvent;

        /// <summary>
        /// Start에서 엔티티를 초기화합니다.
        /// 서브클래스에서 override할 때는 maxHp 조정 후 base.Start()를 호출하세요.
        /// </summary>
        protected virtual void Start()
        {
            _entity = new RuntimeEntity(entityDisplayName, maxHp);
            _entity.OnDeath += () =>
            {
                OnDeathEvent?.Invoke();
                HandleDeath();
            };
        }

        public virtual void TakeDamage(float amount) => _entity.TakeDamage(amount);
        public virtual void Heal(float amount)        => _entity.Heal(amount);

        /// <summary>사망 처리. 기본 동작은 GameObject 파괴.</summary>
        protected virtual void HandleDeath() => Destroy(gameObject);
    }
}
