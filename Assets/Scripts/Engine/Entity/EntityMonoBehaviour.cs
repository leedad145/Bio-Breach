// =============================================================================
// EntityMonoBehaviour.cs - 모든 게임 엔티티의 네트워크 기반 클래스
// =============================================================================
// HP는 NetworkVariable<float>로 Server→Client 자동 동기화.
// TakeDamage/Heal: 서버 권위형 — Client에서 호출 시 ServerRpc 경유.
// HandleDeath: Server에서 NetworkObject.Despawn(), 모든 클라이언트에서 이벤트 발생.
// =============================================================================

using System;
using UnityEngine;
using Unity.Netcode;

namespace BioBreach.Engine.Entity
{
    public enum TargetPriority
    {
        Nearest,
        LowestHp,
        HighestPriority
    }

    public abstract class EntityMonoBehaviour : NetworkBehaviour
    {
        [Header("엔티티 기본 스탯")]
        [SerializeField] string entityDisplayName = "Entity";
        [SerializeField] protected float maxHp = 100f;

        [Header("타겟 우선순위 점수")]
        [Tooltip("높을수록 적에게 우선 공격 받음 (HighestPriority 모드에서 사용)")]
        public int priorityScore = 0;

        // =====================================================================
        // 네트워크 HP — Server 쓰기 전용, 모든 클라이언트 읽기 가능
        // =====================================================================

        private readonly NetworkVariable<float> _netCurrentHp = new(
            0f,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server
        );

        public string EntityName => entityDisplayName;
        public float  CurrentHp  => _netCurrentHp.Value;
        public float  MaxHp      => maxHp;
        public bool   IsAlive    => _netCurrentHp.Value > 0f;

        /// <summary>HP가 0이 될 때 모든 클라이언트에서 발생</summary>
        public event Action OnDeathEvent;

        // =====================================================================
        // NGO 생명주기
        // =====================================================================

        public override void OnNetworkSpawn()
        {
            if (IsServer)
                _netCurrentHp.Value = maxHp;

            _netCurrentHp.OnValueChanged += OnHpChanged;
        }

        public override void OnNetworkDespawn()
        {
            _netCurrentHp.OnValueChanged -= OnHpChanged;
        }

        private void OnHpChanged(float prev, float next)
        {
            if (prev > 0f && next <= 0f)
            {
                OnDeathEvent?.Invoke();
                HandleDeath();
            }
        }

        // =====================================================================
        // 공개 메서드
        // =====================================================================

        /// <summary>데미지 적용. Client에서 호출 시 ServerRpc로 전달.</summary>
        public virtual void TakeDamage(float amount)
        {
            if (IsServer)
                ServerApplyDamage(amount);
            else
                TakeDamageServerRpc(amount);
        }

        public virtual void Heal(float amount)
        {
            if (IsServer)
                _netCurrentHp.Value = Mathf.Min(maxHp, _netCurrentHp.Value + amount);
            else
                HealServerRpc(amount);
        }

        // =====================================================================
        // ServerRpc
        // =====================================================================

        [ServerRpc(RequireOwnership = false)]
        private void TakeDamageServerRpc(float amount) => ServerApplyDamage(amount);

        [ServerRpc(RequireOwnership = false)]
        private void HealServerRpc(float amount)
        {
            _netCurrentHp.Value = Mathf.Min(maxHp, _netCurrentHp.Value + amount);
        }

        private void ServerApplyDamage(float amount)
        {
            if (!IsAlive) return;
            _netCurrentHp.Value = Mathf.Max(0f, _netCurrentHp.Value - amount);
        }

        // =====================================================================
        // 사망 처리
        // =====================================================================

        /// <summary>
        /// HP가 0이 될 때 모든 클라이언트에서 호출.
        /// 기본: Server에서 Despawn.
        /// </summary>
        protected virtual void HandleDeath()
        {
            if (IsServer)
                NetworkObject.Despawn();
        }

        // 서브클래스가 Start()에서 초기화 로직을 유지할 수 있도록 보존
        protected virtual void Start() { }
    }
}
