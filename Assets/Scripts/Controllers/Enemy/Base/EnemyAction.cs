using BioBreach.Engine.Entity;
using UnityEngine;

namespace BioBreach.Controller.Enemy.Base
{
    public class EnemyAction
    {
        EnemyController _enemy;
        EnemyNavigation _nav;

        Vector3 _wanderTarget;
        float _wanderTimer;

        const float WANDER_INTERVAL = 3f;
        const float WANDER_RADIUS = 8f;

        float _strafeTimer;
        float _strafeDir = 1f;

        const float STRAFE_CHANGE = 2.5f;

        Vector3 _smoothMoveTarget;

        public EnemyAction(EnemyController enemy)
        {
            _enemy = enemy;
            _nav = new EnemyNavigation();
            _wanderTarget = enemy.transform.position;
        }

        public void Tick(EnemyState state)
        {
            switch (state)
            {
                case EnemyState.Idle:
                    ExecuteIdle();
                    break;

                case EnemyState.Chase:
                    ExecuteChase();
                    break;

                case EnemyState.Attack:
                    ExecuteAttack();
                    break;
            }
        }

        void ExecuteIdle()
        {
            _wanderTimer += Time.deltaTime;

            if (_wanderTimer >= WANDER_INTERVAL ||
                IsNear(_enemy.transform.position, _wanderTarget, 1f))
            {
                _wanderTimer = 0f;
                _wanderTarget = RandomWanderPoint();
            }

            MoveDirectly(_wanderTarget);
        }

        void ExecuteChase()
        {
            var target = _enemy.CurrentTarget;

            Vector3 goal = target != null
                ? target.transform.position
                : _enemy.transform.position;

            _nav.Tick(
                _enemy.transform.position,
                goal,
                _enemy.digRadius,
                _enemy.defenseLayer
            );

            Vector3 moveTarget = _nav.CurrentTarget ?? goal;

            MoveDirectly(moveTarget);
        }

        void ExecuteAttack()
        {
            var target = _enemy.CurrentTarget;

            if (target == null)
                return;

            Vector3 toTarget = target.transform.position - _enemy.transform.position;

            float distSq = toTarget.sqrMagnitude;
            float atkRangeSq = _enemy.attackRange * _enemy.attackRange;

            if (distSq < atkRangeSq * 0.25f)
            {
                MoveDirectly(
                    _enemy.transform.position
                    - toTarget.normalized * _enemy.attackRange * 0.5f
                );
            }
            else
            {
                _strafeTimer += Time.deltaTime;

                if (_strafeTimer >= STRAFE_CHANGE)
                {
                    _strafeTimer = 0f;
                    _strafeDir = Random.value > 0.5f ? 1f : -1f;
                }

                Vector3 right = Vector3.Cross(toTarget.normalized, Vector3.up);

                Vector3 strafe =
                    right * _strafeDir * _enemy.moveSpeed * 0.5f;

                MoveDirectly(
                    _enemy.transform.position
                    + strafe * Time.deltaTime * 10f
                );
            }

            _enemy.TryAttack(target);
        }

        void MoveDirectly(Vector3 targetPos)
        {
            _smoothMoveTarget =
                Vector3.Lerp(_smoothMoveTarget, targetPos, Time.deltaTime * 8f);

            _enemy.MoveToward(_smoothMoveTarget);
        }

        Vector3 RandomWanderPoint()
        {
            Vector2 rand = Random.insideUnitCircle * WANDER_RADIUS;

            Vector3 offset = new Vector3(rand.x, 0f, rand.y);

            return _enemy.transform.position + offset;
        }

        static bool IsNear(Vector3 a, Vector3 b, float radius)
        {
            return (a - b).sqrMagnitude <= radius * radius;
        }
    }
}