// =============================================================================
// TurretController.cs - 방어 포탑
// 이동 없음. 탐지 범위 내 Enemy 중 우선순위에 따라 타겟 선정 후 공격.
// =============================================================================

using System;
using UnityEngine;
using VContainer;
using Unity.Netcode;
using BioBreach.Engine.Entity;
using BioBreach.Engine.Data;
using BioBreach.Controller.Shared;

namespace BioBreach.Controller.Turret
{
    /// <summary>
    /// 방어 포탑.
    /// <list type="bullet">
    ///   <item>이동 없음</item>
    ///   <item>탐지 범위 내 적 중 우선순위 타겟 선정</item>
    ///   <item>공격 범위 내 진입 시 공격 (쿨다운 적용)</item>
    ///   <item>barrel이 지정된 경우 포신만 타겟 방향으로 회전, 없으면 오브젝트 전체 회전</item>
    /// </list>
    /// </summary>
    public class TurretController : EntityMonoBehaviour
    {
        // =====================================================================
        // Inspector 설정
        // =====================================================================

        [Header("JSON 데이터")]
        [Tooltip("turrets.json 의 id. 반드시 입력해야 스탯이 적용된다.")]
        public string dataId;

        [Header("참조")]
        [Tooltip("Enemy가 있는 레이어")]
        public LayerMask enemyLayer;
        [Tooltip("타겟 방향으로 회전할 Transform. null이면 오브젝트 루트가 회전.")]
        public Transform barrel;

        // ── 스탯 (JSON에서 로드, Inspector 비공개) ──────────────────────────────
        [HideInInspector] public float          detectionRange = 20f;
        [HideInInspector] public float          attackRange    = 18f;
        [HideInInspector] public float          attackDamage   = 15f;
        [HideInInspector] public float          attackCooldown = 0.8f;
        [HideInInspector] public TargetPriority targetPriority = TargetPriority.Nearest;

        // =====================================================================
        // 네트워크 포신 회전 동기화 (Server → 모든 클라이언트)
        // =====================================================================

        private readonly NetworkVariable<Quaternion> _netBarrelRot = new(
            Quaternion.identity,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

        // =====================================================================
        // 내부 변수
        // =====================================================================

        float            _lastAttackTime;
        TurretRepository _turretRepo;

        [Inject]
        public void Construct(TurretRepository turretRepo) => _turretRepo = turretRepo;

        // =====================================================================
        // 초기화
        // =====================================================================

        protected override void Start()
        {
            // OnNetworkSpawn 이전에 maxHp를 설정해야 NetworkVariable 초기값에 반영됨
            if (!string.IsNullOrEmpty(dataId) && _turretRepo != null)
            {
                if (_turretRepo.TryGet(dataId, out var d))
                {
                    maxHp          = d.maxHp;
                    detectionRange = d.detectionRange;
                    attackRange    = d.attackRange;
                    attackDamage   = d.attackDamage;
                    attackCooldown = d.attackCooldown;
                    targetPriority = Enum.Parse<TargetPriority>(d.targetPriority, ignoreCase: true);
                }
            }
        }

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn(); // EntityMonoBehaviour HP 초기화

            // 클라이언트: 초기 포신 회전 적용
            if (!IsServer)
            {
                Transform pivot = barrel != null ? barrel : transform;
                pivot.rotation = _netBarrelRot.Value;
            }
        }

        // =====================================================================
        // 업데이트
        // =====================================================================

        void Update()
        {
            if (!IsSpawned) return;

            if (IsServer)
            {
                if (!IsAlive) return;

                var target = SelectTarget();
                if (target == null) return;

                AimAt(target.transform.position);
                TryAttack(target);
            }
            else
            {
                // 클라이언트: 서버 포신 회전으로 보간
                Transform pivot = barrel != null ? barrel : transform;
                pivot.rotation = Quaternion.Slerp(pivot.rotation, _netBarrelRot.Value, Time.deltaTime * 10f);
            }
        }

        // =====================================================================
        // 타겟 선택 — TargetSelector 공유 유틸리티 위임
        // =====================================================================

        EntityMonoBehaviour SelectTarget()
            => TargetSelector.FindTarget(transform.position, detectionRange, enemyLayer, targetPriority);

        // =====================================================================
        // 조준 & 공격
        // =====================================================================

        void AimAt(Vector3 targetPos)
        {
            Transform pivot = barrel != null ? barrel : transform;
            Vector3   dir   = targetPos - pivot.position;
            if (dir.sqrMagnitude > 0.001f)
            {
                pivot.rotation    = Quaternion.LookRotation(dir);
                _netBarrelRot.Value = pivot.rotation; // 클라이언트에 포신 회전 동기화
            }
        }

        void TryAttack(EntityMonoBehaviour target)
        {
            if (target == null || !target.IsAlive) return;
            if ((transform.position - target.transform.position).sqrMagnitude > attackRange * attackRange) return;
            if (Time.time - _lastAttackTime < attackCooldown) return;

            target.TakeDamage(attackDamage);
            _lastAttackTime = Time.time;
        }

        // =====================================================================
        // 기즈모 (에디터 전용)
        // =====================================================================

        void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(transform.position, detectionRange);
            Gizmos.color = Color.blue;
            Gizmos.DrawWireSphere(transform.position, attackRange);
        }
    }
}
