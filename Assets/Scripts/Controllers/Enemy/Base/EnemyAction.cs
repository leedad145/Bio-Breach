using UnityEngine;

namespace BioBreach.Controller.Enemy.Base
{
    public class EnemyAction
    {
        readonly EnemyController _enemy;

        Vector3 _wanderTarget;
        float   _lastWanderTime = float.NegativeInfinity;
        const float WANDER_INTERVAL = 4f;
        const float WANDER_RADIUS   = 8f;

        public EnemyAction(EnemyController enemy)
        {
            _enemy        = enemy;
            _wanderTarget = enemy.transform.position;
        }

        public void Tick(EnemyState state)
        {
            switch (state)
            {
                case EnemyState.Idle:   ExecuteIdle();   break;
                case EnemyState.Chase:  ExecuteChase();  break;
                case EnemyState.Attack: ExecuteAttack(); break;
            }
        }

        // ── 배회 ─────────────────────────────────────────────────────────────
        void ExecuteIdle()
        {
            // Time.time 비교로 스태거링에 영향받지 않는 정확한 간격 유지
            bool reached = (_enemy.transform.position - _wanderTarget).sqrMagnitude < 1f;
            if (Time.time - _lastWanderTime >= WANDER_INTERVAL || reached)
            {
                _lastWanderTime = Time.time;
                _wanderTarget   = RandomWanderPoint();
            }

            _enemy.SetNavDestination(_wanderTarget);
        }

        // ── 추격 ─────────────────────────────────────────────────────────────
        void ExecuteChase()
        {
            var     target = _enemy.CurrentTarget;
            Vector3 goal   = target != null
                ? target.transform.position
                : _enemy.transform.position;

            _enemy.SetNavDestination(goal);

            // 정면 복셀을 무조건 파낸다 (공기여도 OK, 쿨다운 내부에서 제한)
            // → 벽이 있으면 터널을 만들고, NavMesh 재베이크 후 그 경로를 이용
            _enemy.TryDigForward();
        }

        // ── 공격 ─────────────────────────────────────────────────────────────
        void ExecuteAttack()
        {
            var target = _enemy.CurrentTarget;
            if (target == null) return;

            _enemy.SetNavDestination(target.transform.position);
            _enemy.TryAttack(target);
        }

        // ── 유틸 ─────────────────────────────────────────────────────────────
        Vector3 RandomWanderPoint()
        {
            Vector2 rand   = Random.insideUnitCircle * WANDER_RADIUS;
            Vector3 offset = new Vector3(rand.x, 0f, rand.y);
            return _enemy.transform.position + offset;
        }
    }
}
