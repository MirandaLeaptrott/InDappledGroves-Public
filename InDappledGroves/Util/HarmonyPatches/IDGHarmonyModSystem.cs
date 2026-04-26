using Vintagestory.API.Common;
using Vintagestory.API.Server;
using Vintagestory.API.MathTools;
using HarmonyLib;
using System.Text;
using System;
using Vintagestory.GameContent;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Client;
using System.Reflection;
using System.Linq;
using System.Collections.Generic;


namespace InDappledGroves.Util.HarmonyPatches
{
    public class IDGHarmonyModSystem : ModSystem
    {
        private Harmony harmony;
        private readonly string harmonyId = "vinternacht.IDGPatches";

        public override void Start(ICoreAPI api)
        {
            PatchGame();
            base.Start(api);
        }
        private void PatchGame()
        {
            harmony = new Harmony(harmonyId);
            harmony.Patch(typeof(ItemHammer).GetMethod("GetToolModes", BindingFlags.Instance | BindingFlags.Public),
                prefix: new HarmonyMethod(typeof(IDGHarmonyModSystem).GetMethod("onHammerToolModesPrefix", BindingFlags.Static | BindingFlags.Public))
            );
            harmony.PatchAll();
        }


        [HarmonyPrefix]
        [HarmonyPatch(typeof(ItemHammer), "GetToolModes")]
        public static bool onHammerToolModesPrefix(ItemHammer __instance, ItemSlot slot, IClientPlayer forPlayer, BlockSelection blockSel, ref SkillItem[] __result)
        {
            var hammerToolModes = AccessTools.FieldRefAccess<ItemHammer, SkillItem[]>("toolModes");

            if (blockSel == null)
            {
                __result = null;
                return false;
            }

            for (int i = 0; i < __instance.CollectibleBehaviors.Length; i++)
            {
                SkillItem[] result = __instance.CollectibleBehaviors[i].GetToolModes(slot, forPlayer, blockSel);
                List<SkillItem> newToolModes = hammerToolModes(__instance).ToList();

                if (result != null)
                {

                    if (!(forPlayer.Entity.World.BlockAccessor.GetBlock(blockSel.Position) is BlockAnvil))
                    {
                        __result = result;
                        return false;
                    }
                    else
                    {
                        //newToolModes.AddRange(result);
                        __result = newToolModes.ToArray();
                        return false;
                    }
                }
            }
            return false;
        }


        public override void Dispose()
        {
            harmony?.UnpatchAll();
            harmony = null;

        }
    }
}
