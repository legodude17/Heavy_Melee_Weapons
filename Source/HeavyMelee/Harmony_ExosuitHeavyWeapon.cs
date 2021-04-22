﻿using HarmonyLib;
using JetBrains.Annotations;
using System;
using System.Reflection.Emit;
using Verse.AI;
using RimWorld;
using RimWorld.Planet;
using Verse;
using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HeavyWeapons;
using VFECore;

namespace HeavyMelee
{
	[StaticConstructorOnStartup]
    public static class Harmony_ExosuitHeavyWeapon
    {
		public static HashSet<string> SA_HeavyWeaponHediffString = new HashSet<string>();
		public static HashSet<HediffDef> SA_HeavyWeaponableHediffDefs = new HashSet<HediffDef>();
		public static HashSet<string> SA_HeavyWeaponThingString = new HashSet<string>();
        public static HashSet<ThingDef> SA_HeavyWeaponThingDefs = new HashSet<ThingDef>();
		public static HashSet<HeavyWeapon> SA_HeavyWeaponExtentionInstances = new HashSet<HeavyWeapon>();//each of the heavy weapon has a unique instance of this class, so this can be used to keep track of which weapon
        static Harmony_ExosuitHeavyWeapon()
        {
            SA_HeavyWeaponHediffString.Add("ExoskeletonSuit");
            SA_HeavyWeaponHediffString.Add("Trunken_hediff_ExoskeletonArmor");
            SA_HeavyWeaponHediffString.Add("FSFImplantTorsoWorker");
            SA_HeavyWeaponHediffString.Add("FSFImplantTorsoSpeed");
            SA_HeavyWeaponHediffString.Add("FSFImplantTorsoPsychic");
            foreach(string str in SA_HeavyWeaponHediffString){
                HediffDef hdd = DefDatabase<HediffDef>.GetNamed(str, false);
                SA_HeavyWeaponableHediffDefs.Add(hdd);
            }

            SA_HeavyWeaponThingString.Add("HMW_MeleeWeapon_HeavyMonoSword");
            SA_HeavyWeaponThingString.Add("HMW_MeleeWeapon_FlameLance");
            SA_HeavyWeaponThingString.Add("HMW_MeleeWeapon_PsychicWarhammer");
            SA_HeavyWeaponThingString.Add("HMW_MeleeWeapon_ZeusSword");
            SA_HeavyWeaponThingString.Add("HMW_MeleeWeapon_RocketHammer");
            SA_HeavyWeaponThingString.Add("HMW_MeleeWeapon_SagittariusMight");
            SA_HeavyWeaponThingString.Add("HMW_MeleeWeapon_PersonaHeavyMonoSword");
            SA_HeavyWeaponThingString.Add("HMW_MeleeWeapon_PersonaFlameLance");
            SA_HeavyWeaponThingString.Add("HMW_MeleeWeapon_PersonaPsychicWarhammer");
            SA_HeavyWeaponThingString.Add("HMW_MeleeWeapon_PersonaZeusSword");
            //SA_HeavyWeaponThingString.Add("HMW_GuardianShield");
            
            foreach(string str in SA_HeavyWeaponThingString){
                ThingDef hdd = DefDatabase<ThingDef>.GetNamed(str, false);
                if(hdd == null){
                    //Log.Warning("Could not find the def of " + str);
                    continue;
                }
                SA_HeavyWeaponThingDefs.Add(hdd);
                HeavyWeapon HW = hdd.GetModExtension<HeavyWeapon>();
                if(HW == null){
                    //Log.Warning(str + " has no HeavyWeapon extention !");
                }else{
                    SA_HeavyWeaponExtentionInstances.Add(HW);
                }
            }

            HarmonyLib.Harmony harmony = new HarmonyLib.Harmony("ExoHW.ExosuitHeavyWeapon");
            //harmony.PatchAll();
			
			harmony.Patch(
                AccessTools.Method(typeof(HeavyWeapons.Patch_FloatMenuMakerMap.AddHumanlikeOrders_Fix), "CanEquip"),
				null,
                new HarmonyMethod(typeof(Harmony_ExosuitHeavyWeapon), nameof(CanEquipPostFix)),
                null);
            
			harmony.Patch(
                AccessTools.Method(typeof(Pawn_EquipmentTracker), "GetGizmos"),
				null,
                new HarmonyMethod(typeof(Harmony_ExosuitHeavyWeapon), nameof(GetExtraEquipmentGizmosPassThrough)),
                null);

			harmony.Patch(
                AccessTools.Method(typeof(Thing), "TakeDamage"),
                new HarmonyMethod(typeof(Harmony_ExosuitHeavyWeapon), nameof(TakeDamageExtendedShield)),
                null,
                null);

            harmony.Patch(
                AccessTools.Method(typeof(Apparel_Shield), "DrawWornExtras"),
                null,
                new HarmonyMethod(typeof(Harmony_ExosuitHeavyWeapon), nameof(DrawWornExtraPost))
            );
        }
        public static void DrawWornExtraPost(Apparel_Shield __instance){
            __instance.TryGetComp<Comp_ExtendedShield>()?.PostDraw();
        }

		public static IEnumerable<Gizmo> GetExtraEquipmentGizmosPassThrough(IEnumerable<Gizmo> values, Pawn_EquipmentTracker __instance)
		{
			foreach(Gizmo giz in values){
				yield return giz;
			}
			if(PawnAttackGizmoUtility.CanShowEquipmentGizmos() && __instance.pawn.IsColonistPlayerControlled){
				List<ThingWithComps> list = __instance.AllEquipmentListForReading;
				for (int i = 0; i < list.Count; i++){
					ThingWithComps eq = list[i];
                    SagittariusMightPlantModExtention smp = eq.def.GetModExtension<SagittariusMightPlantModExtention>();
                    if(smp != null){
                        yield return new Command_Action {
                            defaultLabel = smp.label,
                            defaultDesc = smp.description,
                            icon = ContentFinder<Texture2D>.Get(smp.texPath, true),
                            action = delegate (){
                                Thing bb = ThingMaker.MakeThing(GravityLanceDefOf.PlantedGravityLance, null);
                                GenSpawn.Spawn(bb, __instance.pawn.Position, __instance.pawn.Map);
                                eq.Destroy();
                            }
                        };
                    }
                }
			}
			yield break;
		}

        public static void CanEquipPostFix(Pawn pawn, HeavyWeapon options, ref bool __result) {
            if (!__result && SA_HeavyWeaponExtentionInstances.Contains(options)) {
                if (pawn != null && pawn.health != null) {
                    foreach(Hediff hed in pawn.health.hediffSet.hediffs){
                        if(SA_HeavyWeaponableHediffDefs.Contains(hed.def)){
                            __result = true;
                            return;
                        }
                    }
                }
            }
        }

        public static bool canBeShielded(Pawn p){
            return p.Faction != null;
        }

        public static List<Thing> SA_PawnsCheck = new List<Thing>();
        public static List<Apparel> SA_ConcurrencyErrorSafetyNet = new List<Apparel>();
        public static IEnumerable<Apparel> DefenderPawnShields(Pawn basePawn, DamageInfo di){
            Map map = basePawn.Map;
            int dirIntForm = (int)(di.Angle * 16 / 360.0f) % 16;
            Vector3 checkVector1;
            Vector3 checkVector2;
            Vector3 checkVector3;
            switch(dirIntForm){
                case 15: //east
                case 0:{
                    checkVector2 = new Vector3(0, 0, -1);
                    break;
                }
                case 1:
                case 2:{
                    checkVector2 = new Vector3(-1, 0, -1);
                    break;
                }
                case 3: //north
                case 4:{
                    checkVector2 = new Vector3(-1, 0, 0);
                    break;
                }
                case 5:
                case 6:{
                    checkVector2 = new Vector3(-1, 0, 1);
                    break;
                }
                case 7: //west
                case 8:{
                    checkVector2 = new Vector3(0, 0, 1);
                    break;
                }
                case 9:
                case 10:{
                    checkVector2 = new Vector3(1, 0, 1);
                    break;
                }
                case 11: //south
                case 12:{
                    checkVector2 = new Vector3(1, 0, 0);
                    break;
                }
                case 13:
                case 14:{
                    checkVector2 = new Vector3(1, 0, -1);
                    break;
                }
                default:{
                    Log.Warning("Shield Calculation : Angle out of range");
                    checkVector1 = default(Vector3);
                    checkVector2 = default(Vector3);
                    checkVector3 = default(Vector3);
                    break;
                }
            }
            //Log.Warning("Angle Calc " + checkVector2 + " / " + di.Angle);
            SA_PawnsCheck.Clear();
            SA_ConcurrencyErrorSafetyNet.Clear();
            Faction baseFaction = basePawn.Faction;
            IntVec3 iv3 = checkVector2.ToIntVec3() + basePawn.Position;
            //SA_PawnsCheck.AddRange(map.thingGrid.ThingsAt(iv3));
            for(int i = 0; i < 9; i++){
                SA_PawnsCheck.AddRange(map.thingGrid.ThingsAt(basePawn.Position + new IntVec3((i / 3) - 1, 0, (i % 3) - 1)));
            }
            //SA_PawnsCheck.AddRange(map.thingGrid.ThingsAt(basePawn.Position));
            foreach(Thing thing in SA_PawnsCheck){
                if(thing is Pawn pawn && pawn.apparel != null && pawn.Faction == baseFaction && !pawn.Downed){
                    foreach(Apparel app in pawn.apparel.WornApparel){
                        Comp_ExtendedShield compC = app.TryGetComp<Comp_ExtendedShield>();
                        if(compC != null && compC.shieldActive && app is ShieldBeltExtended abc && abc.ShieldState == ShieldState.Active){
                            SA_ConcurrencyErrorSafetyNet.Add(app);
                        }
                    }
                }
            }
            foreach(Apparel app in SA_ConcurrencyErrorSafetyNet){
                yield return app;
            }
            yield break;
        }

        public static bool TakeDamageExtendedShield(Thing __instance, DamageInfo dinfo, ref DamageWorker.DamageResult __result){
            if(__instance is Pawn p){
                float amountNow = dinfo.Amount;
                foreach(ShieldBeltExtended damageMe in DefenderPawnShields(p, dinfo)){
                    //int damage = Convert.ToInt32(Mathf.Min(amountNow, damageMe.HitPoints));
                    //damageMe.HitPoints -= damage;
                    //DamageInfo ddClone = new DamageInfo(dinfo);
                    //ddClone.SetAmount(damage);
                    //damageMe.TakeDamage(ddClone);
                    /**if(damageMe.HitPoints <= 0){
                        damageMe.Destroy();
                    }**/
                    //amountNow -= damage;
                    if(damageMe.CheckPreAbsorbDamage(dinfo)){
                        amountNow = 0;
                        break;
                    }
                }
                //dinfo.SetAmount(amountNow);
                if(amountNow == 0){
                    __result = new DamageWorker.DamageResult();
                    return false;
                }
            }
            return true;
        }

    }
}