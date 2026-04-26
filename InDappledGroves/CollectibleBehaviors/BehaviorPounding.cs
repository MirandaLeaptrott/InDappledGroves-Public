using InDappledGroves.Interfaces;
using System;
using System.Collections.Generic;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Util;
using static InDappledGroves.Util.RecipeTools.IDGRecipeNames;

namespace InDappledGroves.CollectibleBehaviors
{
    class BehaviorPounding : CollectibleBehavior, IBehaviorVariant
    {
        ICoreAPI api;
        ICoreClientAPI capi;
        public InventoryBase Inventory { get; }
        public string InventoryClassName => "worldinventory";
        public SkillItem[] toolModes;

        public override void Initialize(JsonObject properties)
        {
            base.Initialize(properties);
        }

        public BehaviorPounding(CollectibleObject collObj) : base(collObj)
        {
            this.collObj = collObj;
            Inventory = new InventoryGeneric(1, "poundingtool-slot", null, null);
        }

        public SkillItem[] GetSkillItems()
        {
            return toolModes ?? new SkillItem[] { null };
        }
        public override void OnLoaded(ICoreAPI api)
        {
            this.api = api;
            this.capi = (api as ICoreClientAPI);



            this.toolModes = ObjectCacheUtil.GetOrCreate<SkillItem[]>(api, "idgPoundModes", delegate
            {

                SkillItem[] array;
                array = new SkillItem[]
                {
                        new SkillItem
                        {
                            Code = new AssetLocation("pounding"),
                            Name = Lang.Get("Pound", Array.Empty<object>())
                        }
                };

                if (capi != null)
                {
                    for (int i = 0; i < array.Length; i++)
                    {
                        array[i].WithIcon(capi, capi.Gui.LoadSvgWithPadding(new AssetLocation("indappledgroves:textures/icons/" + array[i].Code.FirstCodePart().ToString() + ".svg"), 48, 48, 0, color: ColorUtil.Hex2Int("#ffffff")));
                    }
                }

                return array;
            });
        }

    }
}

