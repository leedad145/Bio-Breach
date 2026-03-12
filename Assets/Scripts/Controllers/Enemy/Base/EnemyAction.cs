using UnityEngine;

namespace BioBreach.Controller.Enemy.Base
{
    public class EnemyAction
    {
        readonly EnemyController _enemy;

        Vector3 _wanderTarget;
        float   _wanderTimer;
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
            _wanderTimer += Time.deltaTime;

            bool reached = (_enemy.transform.position - _wanderTarget).sqrMagnitude < 1f;
            if (_wanderTimer >= WANDER_INTERVAL || reached)
            {
                _wanderTimer  = 0f;
                _wanderTarget = RandomWanderPoint();
            }

            _enemy.SetNavDestination(_wanderTarget);
        }

        // ── 추격 ─────────────────────────────────────────────────────────────
        void ExecuteChase()
        {
            var target = _enemy.CurrentTarget;
            Vector3 goal = target != null
                ? target.transform.position
                : _enemy.transform.position;

            _enemy.SetNavDestination(goal);

            // NavMesh 경로가 막혔으면 파기 fallback
            if (_enemy.IsNavPathBlocked())
                _enemy.TryDigToward(goal - _enemy.transform.position);
        }

        // ── 공격 ─────────────────────────────────────────────────────────────
        void ExecuteAttack()
        {
            var target = _enemy.CurrentTarget;
            if (target == null) return;

            // 공격 범위로 계속 접근
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
