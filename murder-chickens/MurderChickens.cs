using System;

namespace Oxide.Plugins
{
    [Info("Murder Chickens", "nimro", "1.0.0")]
    [Description("Ultra-agressive and strong murder chickens")]
    public class MurderChickens : RustPlugin
    {
        /* Base stats */
        private readonly int _health = 400;
        private readonly int _damage = 40;
        // Stamina to use per attack
        private readonly float _attackCost = 0.0f;

        /* AI Behaviour stats */
        // How likely are we to be offensive without being threatened
        private readonly float _hostility = 1.0f;
        // How likely are we to defend ourselves when attacked
        private readonly float _defensiveness = 1.0f;
        // List of the types of Npc that we are afraid of
        private readonly BaseNpc.AiStatistics.FamilyEnum[] _isAfraidOf = Array.Empty<BaseNpc.AiStatistics.FamilyEnum>();
        // The range at which we will engage targets
        private readonly float _aggroRange = 50f;
        // The threshold of our health fraction where there's a chance that we want to fle
        private readonly float _healthFleeThreshold = 0.0f;

        void OnEntitySpawned(Chicken chicken)
        {
            if (chicken == null)
            {
                return;
            }

            chicken.InitializeHealth(_health, _health);
            chicken.lifestate = BaseCombatEntity.LifeState.Alive;
            chicken.AttackDamage = _damage;
            chicken.AttackCost = _attackCost;

            chicken.Stats.Hostility = _hostility;
            chicken.Stats.Defensiveness = _defensiveness;
            chicken.Stats.IsAfraidOf = _isAfraidOf;
            chicken.Stats.AggressionRange = _aggroRange;
            chicken.Stats.HealthThresholdForFleeing = _healthFleeThreshold;
        }
    }
}