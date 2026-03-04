// =============================================================================
// EntityBase.cs - 모든 게임 엔티티의 공통 기반
// Model 레이어 — UnityEngine 의존성 없음 (순수 C#)
// =============================================================================

using System;

namespace BioBreach.Core.Entity
{
    /// <summary>
    /// 적 유닛(면역 세포), 변이 타워, 아이템 등
    /// 모든 게임 엔티티의 공통 기반 클래스.
    /// </summary>
    public abstract class EntityBase
    {
        public string DisplayName  { get; }
        public float  MaxHp        { get; }
        public float  CurrentHp    { get; private set; }
        public bool   IsAlive      => CurrentHp > 0f;

        /// <summary>HP가 0이 되는 순간 한 번 발생</summary>
        public event Action OnDeath;

        protected EntityBase(string displayName, float maxHp)
        {
            DisplayName = displayName;
            MaxHp       = maxHp;
            CurrentHp   = maxHp;
        }

        /// <summary>피해 적용. amount &lt;= 0 이거나 이미 사망 시 무시.</summary>
        public virtual void TakeDamage(float amount)
        {
            if (!IsAlive || amount <= 0f) return;
            CurrentHp = (float)Math.Max(0.0, CurrentHp - amount);
            if (!IsAlive) OnDeath?.Invoke();
        }

        /// <summary>회복. MaxHp 초과 불가. amount &lt;= 0 이거나 사망 시 무시.</summary>
        public virtual void Heal(float amount)
        {
            if (!IsAlive || amount <= 0f) return;
            CurrentHp = (float)Math.Min(MaxHp, CurrentHp + amount);
        }
    }
}
