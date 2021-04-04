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
        private readonly float _stamina = 200;
        
        // Stamina to use per attack
        private readonly float _attackCost = 0;

        /* AI Behaviour stats */

        // How likely are we to be offensive without being threatened
        private readonly float _hostility = 0.9f;
        // How likely are we to defend ourselves when attacked
        private readonly float _defensiveness = 0.9f;
        // List of the types of Npc that we are afraid of
        private readonly BaseNpc.AiStatistics.FamilyEnum[] _isAfraidOf = Array.Empty<BaseNpc.AiStatistics.FamilyEnum>();
        // The family this npc belong to. Npcs in the same family will not attack each other.
        private readonly BaseNpc.AiStatistics.FamilyEnum _family = BaseNpc.AiStatistics.FamilyEnum.Chicken;
        // The range at which we will engage targets
        private readonly float _aggroRange = 150f;
        // The range at which an aggrified npc will disengage it's current target
        private readonly float _deaggroRange = 150f;
        // For how long will we chase a target until we give up (seconds)
        private readonly float _deaggroChaseTime = 30f;
        // When we deaggro, how long do we wait until we can aggro again
        private readonly float _deaggroCooldown = 0;
        // The threshold of our health fraction where there's a chance that we want to fle
        private readonly float _healthFleeThreshold = 0.2f;
        // The chance that we will flee when our health threshold is triggered
        private readonly float _healthFleeChance = 0.33f;

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
            chicken.Stamina = new VitalLevel() { Level = _stamina };

            chicken.Stats.Family = _family;
            chicken.Stats.Hostility = _hostility;
            chicken.Stats.Defensiveness = _defensiveness;
            chicken.Stats.IsAfraidOf = _isAfraidOf;
            chicken.Stats.AggressionRange = _aggroRange;
            chicken.Stats.DeaggroRange = _deaggroRange;
            chicken.Stats.DeaggroChaseTime = _deaggroChaseTime;
            chicken.Stats.DeaggroCooldown = _deaggroCooldown;
            chicken.Stats.HealthThresholdForFleeing = _healthFleeThreshold;
            chicken.Stats.HealthThresholdFleeChance = _healthFleeChance;
        }
    }
}