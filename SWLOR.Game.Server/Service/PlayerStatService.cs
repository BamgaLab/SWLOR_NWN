﻿
using NWN;
using SWLOR.Game.Server.Enumeration;
using SWLOR.Game.Server.GameObject;

using System;
using System.Collections.Generic;
using System.Linq;
using SWLOR.Game.Server.Data.Entity;
using SWLOR.Game.Server.NWNX;
using static NWN._;
using SWLOR.Game.Server.ValueObject;

namespace SWLOR.Game.Server.Service
{
    public static class PlayerStatService
    {
        public const float PrimaryIncrease = 0.1f;
        public const float SecondaryIncrease = 0.05f;
        public const float TertiaryIncrease = 0.025f;
        private const int MaxAttributeBonus = 35;
        
        public static void ApplyStatChanges(NWPlayer player, NWItem ignoreItem, bool isInitialization = false)
        {
            if (!player.IsPlayer) return;
            if (!player.IsInitializedAsPlayer) return;
            if (player.GetLocalInt("IS_SHIP") == 1) return;

            // Don't fire for ammo as it reapplies bonuses **just** removed from blasters.
            if (ignoreItem != null &&
                (ignoreItem.BaseItemType == BASE_ITEM_BOLT ||
                 ignoreItem.BaseItemType == BASE_ITEM_ARROW ||
                 ignoreItem.BaseItemType == BASE_ITEM_BULLET)) return;

            Player pcEntity = DataService.Get<Player>(player.GlobalID);
            List<PCSkill> skills = DataService.Where<PCSkill>(x => x.PlayerID == player.GlobalID && x.Rank > 0).ToList();
            EffectiveItemStats itemBonuses = GetPlayerItemEffectiveStats(player, ignoreItem);
            
            float strBonus = 0.0f;
            float dexBonus = 0.0f;
            float conBonus = 0.0f;
            float intBonus = 0.0f;
            float wisBonus = 0.0f;
            float chaBonus = 0.0f;

            using (new Profiler("PlayerStatService::ApplyStatChanges::AttributeApplication"))
            {
                foreach (PCSkill pcSkill in skills)
                {
                    Skill skill = DataService.Get<Skill>(pcSkill.SkillID);
                    CustomAttribute primary = (CustomAttribute) skill.Primary;
                    CustomAttribute secondary = (CustomAttribute) skill.Secondary;
                    CustomAttribute tertiary = (CustomAttribute) skill.Tertiary;

                    // Primary Bonuses
                    if (primary == CustomAttribute.STR) strBonus += PrimaryIncrease * pcSkill.Rank;
                    else if (primary == CustomAttribute.DEX) dexBonus += PrimaryIncrease * pcSkill.Rank;
                    else if (primary == CustomAttribute.CON) conBonus += PrimaryIncrease * pcSkill.Rank;
                    else if (primary == CustomAttribute.INT) intBonus += PrimaryIncrease * pcSkill.Rank;
                    else if (primary == CustomAttribute.WIS) wisBonus += PrimaryIncrease * pcSkill.Rank;
                    else if (primary == CustomAttribute.CHA) chaBonus += PrimaryIncrease * pcSkill.Rank;

                    // Secondary Bonuses
                    if (secondary == CustomAttribute.STR) strBonus += SecondaryIncrease * pcSkill.Rank;
                    else if (secondary == CustomAttribute.DEX) dexBonus += SecondaryIncrease * pcSkill.Rank;
                    else if (secondary == CustomAttribute.CON) conBonus += SecondaryIncrease * pcSkill.Rank;
                    else if (secondary == CustomAttribute.INT) intBonus += SecondaryIncrease * pcSkill.Rank;
                    else if (secondary == CustomAttribute.WIS) wisBonus += SecondaryIncrease * pcSkill.Rank;
                    else if (secondary == CustomAttribute.CHA) chaBonus += SecondaryIncrease * pcSkill.Rank;

                    // Tertiary Bonuses
                    if (tertiary == CustomAttribute.STR) strBonus += TertiaryIncrease * pcSkill.Rank;
                    else if (tertiary == CustomAttribute.DEX) dexBonus += TertiaryIncrease * pcSkill.Rank;
                    else if (tertiary == CustomAttribute.CON) conBonus += TertiaryIncrease * pcSkill.Rank;
                    else if (tertiary == CustomAttribute.INT) intBonus += TertiaryIncrease * pcSkill.Rank;
                    else if (tertiary == CustomAttribute.WIS) wisBonus += TertiaryIncrease * pcSkill.Rank;
                    else if (tertiary == CustomAttribute.CHA) chaBonus += TertiaryIncrease * pcSkill.Rank;
                }
            }


            // Check caps.
            if (strBonus > MaxAttributeBonus) strBonus = MaxAttributeBonus;
            if (dexBonus > MaxAttributeBonus) dexBonus = MaxAttributeBonus;
            if (conBonus > MaxAttributeBonus) conBonus = MaxAttributeBonus;
            if (intBonus > MaxAttributeBonus) intBonus = MaxAttributeBonus;
            if (wisBonus > MaxAttributeBonus) wisBonus = MaxAttributeBonus;
            if (chaBonus > MaxAttributeBonus) chaBonus = MaxAttributeBonus;

            // Apply item bonuses
            strBonus += itemBonuses.Strength / 3;
            dexBonus += itemBonuses.Dexterity / 3;
            conBonus += itemBonuses.Constitution / 3;
            wisBonus += itemBonuses.Wisdom / 3;
            intBonus += itemBonuses.Intelligence / 3;
            chaBonus += itemBonuses.Charisma / 3;

            // Check final caps
            if (strBonus > 55) strBonus = 55;
            if (dexBonus > 55) dexBonus = 55;
            if (conBonus > 55) conBonus = 55;
            if (intBonus > 55) intBonus = 55;
            if (wisBonus > 55) wisBonus = 55;
            if (chaBonus > 55) chaBonus = 55;

            // Apply attributes
            NWNXCreature.SetRawAbilityScore(player, ABILITY_STRENGTH, (int) strBonus + pcEntity.STRBase);
            NWNXCreature.SetRawAbilityScore(player, ABILITY_DEXTERITY, (int) dexBonus + pcEntity.DEXBase);
            NWNXCreature.SetRawAbilityScore(player, ABILITY_CONSTITUTION, (int) conBonus + pcEntity.CONBase);
            NWNXCreature.SetRawAbilityScore(player, ABILITY_INTELLIGENCE, (int) intBonus + pcEntity.INTBase);
            NWNXCreature.SetRawAbilityScore(player, ABILITY_WISDOM, (int) wisBonus + pcEntity.WISBase);
            NWNXCreature.SetRawAbilityScore(player, ABILITY_CHARISMA, (int) chaBonus + pcEntity.CHABase);

            // Apply AC
            using (new Profiler("PlayerStatService::ApplyStatChanges::CalcAC"))
            {
                int ac = EffectiveArmorClass(player, ignoreItem, itemBonuses);
                NWNXCreature.SetBaseAC(player, ac);
            }

            // Apply BAB
            using (new Profiler("PlayerStatService::ApplyStatChanges::CalcBAB"))
            {
                int bab = CalculateBAB(player, ignoreItem, itemBonuses);
                NWNXCreature.SetBaseAttackBonus(player, bab);
            }

            // Apply HP
            using (new Profiler("PlayerStatService::ApplyStatChanges::CalcHP"))
            {
                int hp = EffectiveMaxHitPoints(player, itemBonuses);
                for (int level = 1; level <= 5; level++)
                {
                    hp--;
                    NWNXCreature.SetMaxHitPointsByLevel(player, level, 1);
                }

                for (int level = 1; level <= 5; level++)
                {
                    if (hp > 255) // Levels can only contain a max of 255 HP
                    {
                        NWNXCreature.SetMaxHitPointsByLevel(player, level, 255);
                        hp = hp - 254;
                    }
                    else // Remaining value gets set to the level. (<255 hp)
                    {
                        NWNXCreature.SetMaxHitPointsByLevel(player, level, hp + 1);
                        break;
                    }
                }
            }

            if (player.CurrentHP > player.MaxHP)
            {
                int amount = player.CurrentHP - player.MaxHP;
                Effect damage = _.EffectDamage(amount);
                _.ApplyEffectToObject(DURATION_TYPE_INSTANT, damage, player.Object);
            }

            // Apply FP
            using (new Profiler("PlayerStatService::ApplyStatChanges::CalcFP"))
            {
                pcEntity.MaxFP = EffectiveMaxFP(player, itemBonuses);

                if (isInitialization)
                {
                    pcEntity.CurrentFP = pcEntity.MaxFP;
                }

                DataService.SubmitDataChange(pcEntity, DatabaseActionType.Update);
            }

            // Attempt a refresh of the character sheet UI in a second.
            _.DelayCommand(1.0f, () =>
            {
                NWNXPlayer.UpdateCharacterSheet(player);
            });
        }


        private static int CalculateAdjustedValue(int baseValue, int recommendedLevel, int skillRank, int minimumValue)
        {
            int adjustedValue = (int)CalculateAdjustedValue((float)baseValue, recommendedLevel, skillRank, minimumValue);
            if (adjustedValue < minimumValue) adjustedValue = minimumValue;
            return adjustedValue;
        }

        private static float CalculateAdjustedValue(float baseValue, int recommendedLevel, int skillRank, float minimumValue)
        {
            int delta = recommendedLevel - skillRank;
            float adjustment = 1.0f - delta * 0.1f;
            if (adjustment <= 0.1f) adjustment = 0.1f;
            else if (adjustment > 1.0f) adjustment = 1.0f;

            float adjustedValue = (float)Math.Round(baseValue * adjustment);
            if (adjustedValue < minimumValue) adjustedValue = minimumValue;
            return adjustedValue;
        }

        private static int EffectiveMaxHitPoints(NWPlayer player, EffectiveItemStats stats)
        {
            int hp = 25 + player.ConstitutionModifier * 5;
            float effectPercentBonus = CustomEffectService.CalculateEffectHPBonusPercent(player);
            
            hp += PerkService.GetCreaturePerkLevel(player, PerkType.Health) * 5;
            hp += stats.HP;
            hp = hp + (int)(hp * effectPercentBonus);

            if (hp > 1275) hp = 1275;
            if (hp < 20) hp = 20;

            return hp;
        }

        private static int EffectiveMaxFP(NWPlayer player, EffectiveItemStats stats)
        {
            int fp = 20;
            fp += (player.IntelligenceModifier + player.WisdomModifier + player.CharismaModifier) * 5;
            fp += PerkService.GetCreaturePerkLevel(player, PerkType.FP) * 5;
            fp += stats.FP;

            if (fp < 0) fp = 0;

            return fp;
        }

        private static int EffectiveArmorClass(NWPlayer player, NWItem ignoreItem, EffectiveItemStats stats)
        {
            int baseAC = stats.AC / 3 + CustomEffectService.CalculateEffectAC(player);

            // Calculate AC bonus granted by skill ranks.
            // Only chest armor is checked for this bonus.
            
            if(ignoreItem != player.Chest)
            {
                CustomItemType armorType = player.Chest.CustomItemType;
                int skillRank = 0;
                switch (armorType)
                {
                    case CustomItemType.LightArmor:
                        skillRank = SkillService.GetPCSkillRank(player, SkillType.LightArmor);
                        break;
                    case CustomItemType.HeavyArmor:
                        skillRank = SkillService.GetPCSkillRank(player, SkillType.HeavyArmor);
                        break;
                    case CustomItemType.ForceArmor:
                        skillRank = SkillService.GetPCSkillRank(player, SkillType.ForceArmor);
                        break;
                }

                // +1 AC per 20 skill ranks, while wearing the appropriate armor.
                int skillACBonus = skillRank / 20;
                baseAC += skillACBonus;
            }

            int totalAC = _.GetAC(player) - baseAC;
            
            // Shield Oath and Precision Targeting affect a percentage of the TOTAL armor class on a creature.
            var stance = CustomEffectService.GetCurrentStanceType(player);
            if (stance == CustomEffectType.ShieldOath)
            {
                int bonus = (int) (totalAC * 0.2f);
                baseAC += bonus;
            }
            else if (stance == CustomEffectType.PrecisionTargeting)
            {
                int penalty = (int)(totalAC * 0.3f);
                baseAC -= penalty;
            }

            if (baseAC < 0) baseAC = 0;

            return baseAC;
        }
        
        public static EffectiveItemStats GetPlayerItemEffectiveStats(NWPlayer player, NWItem ignoreItem = null)
        {
            using (new Profiler("PlayerStatService::ApplyStatChanges::GetPlayerItemEffectiveStats"))
            {
                var pcSkills = DataService.Where<PCSkill>(x => x.PlayerID == player.GlobalID);
                
                int heavyRank = pcSkills.Single(x => x.SkillID == (int)SkillType.HeavyArmor).Rank;
                int lightRank = pcSkills.Single(x => x.SkillID == (int)SkillType.LightArmor).Rank;
                int forceRank = pcSkills.Single(x => x.SkillID == (int)SkillType.ForceArmor).Rank;
                int martialRank = pcSkills.Single(x => x.SkillID == (int)SkillType.MartialArts).Rank;

                EffectiveItemStats stats = new EffectiveItemStats();
                stats.EnmityRate = 1.0f;

                using (new Profiler("PlayerStatService::ApplyStatChanges::GetPlayerItemEffectiveStats::ItemLoop"))
                {
                    HashSet<NWItem> processed = new HashSet<NWItem>();
                    for (int itemSlot = 0; itemSlot < NUM_INVENTORY_SLOTS; itemSlot++)
                    {
                        NWItem item = _.GetItemInSlot(itemSlot, player);

                        if (!item.IsValid || item.Equals(ignoreItem)) continue;
                        SkillType skill = ItemService.GetSkillTypeForItem(item);
                        int rank;

                        // Have we already processed this particular item? Skip over it.
                        // NWN likes to include the same weapon in multiple slots for some reasons, so this works around that.
                        // If someone has a better solution to this please feel free to change it.
                        if (processed.Contains(item)) continue;
                        processed.Add(item);
                        
                        using(new Profiler("PlayerStatService::ApplyStatChanges::GetPlayerItemEffectiveStats::ItemLoop::GetRank"))
                        {
                            rank = pcSkills.Single(x => x.SkillID == (int)skill).Rank;
                        }

                        using (new Profiler("PlayerStatService::ApplyStatChanges::GetPlayerItemEffectiveStats::ItemLoop::StatAdjustments"))
                        {
                            // Only scale cooldown recovery if it's a bonus. Penalties remain regardless of skill level difference.
                            if (item.CooldownRecovery > 0)
                            {
                                stats.CooldownRecovery += CalculateAdjustedValue(item.CooldownRecovery, item.RecommendedLevel, rank, 1);
                            }
                            else stats.CooldownRecovery += item.CooldownRecovery;

                            stats.EnmityRate += CalculateAdjustedValue(0.01f * item.EnmityRate, item.RecommendedLevel, rank, 0.00f);
                            
                            stats.Luck += CalculateAdjustedValue(item.LuckBonus, item.RecommendedLevel, rank, 0);
                            stats.Meditate += CalculateAdjustedValue(item.MeditateBonus, item.RecommendedLevel, rank, 0);
                            stats.Rest += CalculateAdjustedValue(item.RestBonus, item.RecommendedLevel, rank, 0);
                            stats.Medicine += CalculateAdjustedValue(item.MedicineBonus, item.RecommendedLevel, rank, 0);
                            stats.HPRegen += CalculateAdjustedValue(item.HPRegenBonus, item.RecommendedLevel, rank, 0);
                            stats.FPRegen += CalculateAdjustedValue(item.FPRegenBonus, item.RecommendedLevel, rank, 0);
                            stats.Weaponsmith += CalculateAdjustedValue(item.CraftBonusWeaponsmith, item.RecommendedLevel, rank, 0);
                            stats.Cooking += CalculateAdjustedValue(item.CraftBonusCooking, item.RecommendedLevel, rank, 0);
                            stats.Engineering += CalculateAdjustedValue(item.CraftBonusEngineering, item.RecommendedLevel, rank, 0);
                            stats.Fabrication += CalculateAdjustedValue(item.CraftBonusFabrication, item.RecommendedLevel, rank, 0);
                            stats.Armorsmith += CalculateAdjustedValue(item.CraftBonusArmorsmith, item.RecommendedLevel, rank, 0);
                            stats.Harvesting += CalculateAdjustedValue(item.HarvestingBonus, item.RecommendedLevel, rank, 0);
                            stats.Piloting += CalculateAdjustedValue(item.PilotingBonus, item.RecommendedLevel, rank, 0);
                            stats.Scavenging += CalculateAdjustedValue(item.ScavengingBonus, item.RecommendedLevel, rank, 0);
                            stats.SneakAttack += CalculateAdjustedValue(item.SneakAttackBonus, item.RecommendedLevel, rank, 0);

                            stats.Strength += CalculateAdjustedValue(item.StrengthBonus, item.RecommendedLevel, rank, 0);
                            stats.Dexterity += CalculateAdjustedValue(item.DexterityBonus, item.RecommendedLevel, rank, 0);
                            stats.Constitution += CalculateAdjustedValue(item.ConstitutionBonus, item.RecommendedLevel, rank, 0);
                            stats.Wisdom += CalculateAdjustedValue(item.WisdomBonus, item.RecommendedLevel, rank, 0);
                            stats.Intelligence += CalculateAdjustedValue(item.IntelligenceBonus, item.RecommendedLevel, rank, 0);
                            stats.Charisma += CalculateAdjustedValue(item.CharismaBonus, item.RecommendedLevel, rank, 0);
                            stats.HP += CalculateAdjustedValue(item.HPBonus, item.RecommendedLevel, rank, 0);
                            stats.FP += CalculateAdjustedValue(item.FPBonus, item.RecommendedLevel, rank, 0);

                        }

                        using(new Profiler("PlayerStatService::ApplyStatChanges::GetPlayerItemEffectiveStats::ItemLoop::CalcBAB"))
                        {
                            // Calculate base attack bonus
                            if (ItemService.WeaponBaseItemTypes.Contains(item.BaseItemType))
                            {
                                int itemLevel = item.RecommendedLevel;
                                int delta = itemLevel - rank;
                                int itemBAB = item.BaseAttackBonus;
                                if (delta >= 1) itemBAB--;
                                if (delta > 0) itemBAB = itemBAB - delta / 5;

                                if (itemBAB <= 0) itemBAB = 0;
                                stats.BAB += itemBAB;
                            }
                        }

                        using(new Profiler("PlayerStatService::ApplyStatChanges::GetPlayerItemEffectiveStats::ItemLoop::CalcAC"))
                        {
                            // Calculate AC
                            if (ItemService.ArmorBaseItemTypes.Contains(item.BaseItemType))
                            {
                                int skillRankToUse;
                                if (item.CustomItemType == CustomItemType.HeavyArmor)
                                {
                                    skillRankToUse = heavyRank;
                                }
                                else if (item.CustomItemType == CustomItemType.LightArmor)
                                {
                                    skillRankToUse = lightRank;
                                }
                                else if (item.CustomItemType == CustomItemType.ForceArmor)
                                {
                                    skillRankToUse = forceRank;
                                }
                                else if (item.CustomItemType == CustomItemType.MartialArtWeapon)
                                {
                                    skillRankToUse = martialRank;
                                }
                                else continue;

                                int itemAC = item.CustomAC;
                                itemAC = CalculateAdjustedValue(itemAC, item.RecommendedLevel, skillRankToUse, 0);
                                stats.AC += itemAC;
                            }
                        }
                    }
                }

                using (new Profiler("PlayerStatService::ApplyStatChanges::GetPlayerItemEffectiveStats::FinalAdjustments"))
                {
                    // Final casting speed adjustments
                    if (stats.CooldownRecovery < -99)
                        stats.CooldownRecovery = -99;
                    else if (stats.CooldownRecovery > 99)
                        stats.CooldownRecovery = 99;

                    // Final enmity adjustments
                    if (stats.EnmityRate < 0.5f) stats.EnmityRate = 0.5f;
                    else if (stats.EnmityRate > 1.5f) stats.EnmityRate = 1.5f;

                    var stance = CustomEffectService.GetCurrentStanceType(player);
                    if (stance == CustomEffectType.ShieldOath)
                    {
                        stats.EnmityRate = stats.EnmityRate + 0.2f;
                    }

                    return stats;
                }
            }
        }

        private static readonly HashSet<int> ACBaseItemTypes = new HashSet<int>()
        {
            BASE_ITEM_AMULET,
            BASE_ITEM_ARMOR,
            BASE_ITEM_BELT,
            BASE_ITEM_CLOAK,
            BASE_ITEM_HELMET,
            BASE_ITEM_GLOVES,
            BASE_ITEM_BRACER,
            BASE_ITEM_BOOTS,
            BASE_ITEM_LARGESHIELD,
            BASE_ITEM_SMALLSHIELD,
            BASE_ITEM_TOWERSHIELD
        };

        public static float EffectiveResidencyBonus(NWPlayer player)
        {
            var dbPlayer = DataService.Get<Player>(player.GlobalID);

            // Player doesn't have either kind of residence. Return 0f
            if (dbPlayer.PrimaryResidencePCBaseID == null &&
                dbPlayer.PrimaryResidencePCBaseStructureID == null) return 0.0f;

            // Two paths for this. Players can either have a primary residence in an apartment which is considered a "PCBase".
            // Or they can have a primary residence in a building which is a child structure contained in an actual PCBase.
            // We grab the furniture objects differently based on the type.
            
            List<PCBaseStructure> structures;

            // Apartments - Pull structures directly from the table based on the PCBaseID
            if (dbPlayer.PrimaryResidencePCBaseID != null)
            {
                structures = DataService.Where<PCBaseStructure>(x => x.PCBaseID == dbPlayer.PrimaryResidencePCBaseID).ToList();
                
            }
            // Buildings - Get the building's PCBaseID and then grab its children
            else
            {
                structures = DataService.Where<PCBaseStructure>(x => x.ParentPCBaseStructureID == dbPlayer.PrimaryResidencePCBaseStructureID).ToList();
            }

            var atmoStructures = structures.Where(x =>
            {
                var baseStructure = DataService.Get<BaseStructure>(x.BaseStructureID);
                return baseStructure.HasAtmosphere;
            }).ToList();
            
            float bonus = atmoStructures.Sum(x => (x.StructureBonus * 0.02f) + 0.02f);

            if (bonus >= 1.5f) bonus = 1.5f; // Maximum = 250% XP (+150% bonus from residency)
            return bonus;
        }
        
        private static int CalculateBAB(NWPlayer oPC, NWItem ignoreItem, EffectiveItemStats stats)
        {
            NWItem weapon = oPC.RightHand;

            // The unequip event fires before the item is actually unequipped, so we need
            // to have additional checks to make sure we're not getting the weapon that's about to be
            // unequipped.
            if (weapon.Equals(ignoreItem))
            {
                weapon = null;
                NWItem offHand = oPC.LeftHand;

                if (offHand.CustomItemType == CustomItemType.Vibroblade ||
                   offHand.CustomItemType == CustomItemType.FinesseVibroblade ||
                   offHand.CustomItemType == CustomItemType.Baton ||
                   offHand.CustomItemType == CustomItemType.HeavyVibroblade ||
                   offHand.CustomItemType == CustomItemType.Saberstaff ||
                   offHand.CustomItemType == CustomItemType.Polearm ||
                   offHand.CustomItemType == CustomItemType.TwinBlade ||
                   offHand.CustomItemType == CustomItemType.MartialArtWeapon ||
                   offHand.CustomItemType == CustomItemType.BlasterPistol ||
                   offHand.CustomItemType == CustomItemType.BlasterRifle ||
                   offHand.CustomItemType == CustomItemType.Throwing)
                {
                    weapon = offHand;
                }
            }

            if (weapon == null || !weapon.IsValid)
            {
                weapon = oPC.Arms;
            }
            if (!weapon.IsValid) return 0;

            SkillType itemSkill = ItemService.GetSkillTypeForItem(weapon);
            if (itemSkill == SkillType.Unknown ||
                itemSkill == SkillType.LightArmor ||
                itemSkill == SkillType.HeavyArmor ||
                itemSkill == SkillType.ForceArmor ||
                itemSkill == SkillType.Shields) return 0;

            int weaponSkillID = (int)itemSkill;
            PCSkill skill = DataService.Single<PCSkill>(x => x.PlayerID == oPC.GlobalID && x.SkillID == weaponSkillID);
            if (skill == null) return 0;
            int skillBAB = skill.Rank / 10;
            int perkBAB = 0;
            int backgroundBAB = 0;
            BackgroundType background = (BackgroundType)oPC.Class1;
            bool receivesBackgroundBonus = false;
            
            switch (weapon.CustomItemType)
            {
                case CustomItemType.FinesseVibroblade:
                    receivesBackgroundBonus = background == BackgroundType.Duelist;
                    break;
                case CustomItemType.Baton:
                    receivesBackgroundBonus = background == BackgroundType.SecurityOfficer;
                    break;
                case CustomItemType.HeavyVibroblade:
                    receivesBackgroundBonus = background == BackgroundType.Soldier;
                    break;
                case CustomItemType.TwinBlade:
                    receivesBackgroundBonus = background == BackgroundType.Berserker;
                    break;
                case CustomItemType.MartialArtWeapon:
                    receivesBackgroundBonus = background == BackgroundType.TerasKasi;
                    break;
                case CustomItemType.BlasterPistol:
                    receivesBackgroundBonus = background == BackgroundType.Smuggler;
                    break;
                case CustomItemType.BlasterRifle:
                    receivesBackgroundBonus = background == BackgroundType.Sharpshooter || background == BackgroundType.Mandalorian;
                    break;
            }
            
            if (receivesBackgroundBonus)
            {
                backgroundBAB = background == BackgroundType.Mandalorian ? 1 : 2;
            }
            
            return 1 + skillBAB + perkBAB + stats.BAB + backgroundBAB; // Note: Always add 1 to BAB. 0 will cause a crash in NWNX.
        }
    }
}
