namespace BioBreach.Controller.Enemy.Base
{
    public enum EnemyState
    {
        Idle,
        Chase,
        Attack
    }

    public class EnemyBrain
    {
        EnemyState _state;
        EnemyController _enemy;

        public EnemyBrain(EnemyController enemy)
        {
            _enemy = enemy;
        }

        public EnemyState CurrentState => _state;

        public void UpdateBrain()
        {
            var target = _enemy.CurrentTarget;

            switch (_state)
            {
                case EnemyState.Idle:

                    if (target != null)
                        _state = EnemyState.Chase;

                    break;

                case EnemyState.Chase:

                    if (target == null)
                    {
                        _state = EnemyState.Idle;
                        break;
                    }

                    if (_enemy.IsTargetInAttackRange(target))
                        _state = EnemyState.Attack;

                    break;

                case EnemyState.Attack:

                    if (!_enemy.IsTargetInAttackRange(target))
                        _state = EnemyState.Chase;

                    break;
            }
        }
    }
}