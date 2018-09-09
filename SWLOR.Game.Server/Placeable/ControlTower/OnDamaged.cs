﻿using System.Linq;
using NWN;
using SWLOR.Game.Server.Data.Contracts;
using SWLOR.Game.Server.Data.Entities;
using SWLOR.Game.Server.Event;
using SWLOR.Game.Server.GameObject;
using SWLOR.Game.Server.Service.Contracts;
using static NWN.NWScript;

namespace SWLOR.Game.Server.Placeable.ControlTower
{
    public class OnDamaged: IRegisteredEvent
    {
        private readonly INWScript _;
        private readonly IDataContext _db;
        private readonly IRandomService _random;
        private readonly IBaseService _base;

        public OnDamaged(
            INWScript script,
            IDataContext db,
            IRandomService random,
            IBaseService @base)
        {
            _ = script;
            _db = db;
            _random = random;
            _base = @base;
        }

        public bool Run(params object[] args)
        {
            NWCreature attacker = NWCreature.Wrap(_.GetLastDamager(Object.OBJECT_SELF));
            NWPlaceable tower = NWPlaceable.Wrap(Object.OBJECT_SELF);
            NWItem weapon = NWItem.Wrap(_.GetLastWeaponUsed(attacker.Object));
            int damage = _.GetTotalDamageDealt();
            int structureID = tower.GetLocalInt("PC_BASE_STRUCTURE_ID");
            PCBaseStructure structure = _db.PCBaseStructures.Single(x => x.PCBaseStructureID == structureID);
            int maxShieldHP = _base.CalculateMaxShieldHP(structure);
            structure.ShieldHP -= damage;
            if (structure.ShieldHP <= 0) structure.ShieldHP = 0;
            float hpPercentage = (float)structure.ShieldHP / (float)maxShieldHP * 100.0f;

            if (hpPercentage <= 25.0f && structure.ReinforcedFuel > 0)
            {
                structure.IsInReinforcedMode = true;
                structure.ShieldHP = (int)(maxShieldHP * 0.25f);
            }

            attacker.SendMessage("Tower Shields: " + hpPercentage.ToString("0.00") + "%");

            if (structure.IsInReinforcedMode)
            {
                attacker.SendMessage("Control tower is in reinforced mode and cannot be damaged. Reinforced mode will be disabled when the tower runs out of fuel.");
            }

            _.ApplyEffectToObject(DURATION_TYPE_INSTANT, _.EffectHeal(9999), tower.Object);
            weapon.Durability -= _random.RandomFloat(0.01f, 0.03f);

            if (structure.ShieldHP <= 0)
            {
                structure.ShieldHP = 0;
                
                structure.Durability -= _random.RandomFloat(0.5f, 2.0f);
                attacker.SendMessage("Structure Durability: " + structure.Durability.ToString("0.00"));

                if (structure.Durability <= 0.0f)
                {
                    structure.Durability = 0.0f;
                    DestroyTower();
                }
            }


            _db.SaveChanges();
            return true;
        }


        private void DestroyTower()
        {
            // todo: Destroy structures, place contents inside lootable containers
            // todo: also display a huge explosion
        }
    }
}
