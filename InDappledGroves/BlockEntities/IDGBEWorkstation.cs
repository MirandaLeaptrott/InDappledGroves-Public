using System;
using System.Text;
using InDappledGroves.CollectibleBehaviors;
using InDappledGroves.Util.Handlers;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.Client.NoObf;
using Vintagestory.GameContent;
using static InDappledGroves.Util.RecipeTools.IDGRecipeNames;

namespace InDappledGroves.BlockEntities
{
    public class IDGBEWorkstation : BlockEntityDisplay
    {
        protected InventoryGeneric inventory;

        public BlockFacing[] PushFaces = new BlockFacing[0];
        public BlockFacing[] AcceptFromFaces = new BlockFacing[0];

        public override InventoryBase Inventory => inventory;

        public override string InventoryClassName
        {
            get
            {
                if (Block != null) return Block.Attributes["inventoryclass"].AsString();
                return "workstation";
            }
        }

        public override string AttributeTransformCode => Block.Attributes["attributetransformcode"].AsString();

        public string workstationtype => Block.Attributes["workstationproperties"]["workstationtype"].ToString();
        public string processmodifier => Block.Attributes["workstationproperties"]["processmodifiername"].ToString();

        // Removed default = false; decompile shows no initializer (default is false anyway)
        public bool recipecomplete { get; set; }

        public RecipeHandler recipeHandler { get; set; }
        public float currentMiningDamage { get; set; }

        public ItemSlot InputSlot => Inventory[Block.Attributes["workstationproperties"]["slottypes"]["inputslot"].AsInt()];

        public ItemSlot ProcessModifierSlot
        {
            get
            {
                if (Block.Attributes["workstationproperties"]["workstationtype"].ToString() != "complex") return null;
                return Inventory[Block.Attributes["workstationproperties"]["slottypes"]["processmodifier0"].AsInt()];
            }
        }

        public IDGBEWorkstation()
        {
            // Removed extra null param - VS 1.22 InventoryDisplayed constructor signature change
            inventory = new InventoryDisplayed(this, 2, InventoryClassName + "-slot", null);
            inventory.OnGetAutoPushIntoSlot = GetAutoPushIntoSlot;
        }

        // Made private - was unnecessarily public
        private ItemSlot GetAutoPushIntoSlot(BlockFacing atBlockFace, ItemSlot fromSlot)
        {
            recipeHandler.GetMatchingRecipes(Api.World, fromSlot, "any", Block.Attributes["inventoryclass"].ToString(), workstationtype, out var recipe);
            if (recipe == null || InputSlot.StackSize >= InputSlot.MaxSlotStackSize || Array.IndexOf(AcceptFromFaces, atBlockFace) < 0)
            {
                return null;
            }
            return InputSlot;
        }

        public override void OnBlockPlaced(ItemStack byItemStack = null)
        {
            base.OnBlockPlaced(byItemStack);
        }

        public override void Initialize(ICoreAPI api)
        {
            base.Initialize(api);
            if (recipeHandler == null)
            {
                recipeHandler = new RecipeHandler(api, this);
            }
            if (Block.Attributes["pushFaces"].Exists)
            {
                string[] faces = Block.Attributes["pushFaces"].AsArray<string>(null, null);
                PushFaces = new BlockFacing[faces.Length];
                for (int i = 0; i < faces.Length; i++) PushFaces[i] = BlockFacing.FromCode(faces[i]);
            }
            if (Block.Attributes["acceptFromFaces"].Exists)
            {
                string[] faces2 = Block.Attributes["acceptFromFaces"].AsArray<string>(null, null);
                AcceptFromFaces = new BlockFacing[faces2.Length];
                for (int j = 0; j < faces2.Length; j++) AcceptFromFaces[j] = BlockFacing.FromCode(faces2[j]);
            }
            Inventory[0].MaxSlotStackSize = 1;
            Inventory[1].MaxSlotStackSize = 1;
            capi = api as ICoreClientAPI;
        }

        public virtual bool OnInteract(IPlayer byPlayer)
        {
            bool flag = false;
            if (workstationtype == "basic") return OnBasicInteract(byPlayer);
            if (workstationtype == "complex") return OnComplexInteract(byPlayer);
            if (flag)
            {
                updateMeshes();
                MarkDirty(redrawOnClient: true);
            }
            return false;
        }

        public virtual bool OnBasicInteract(IPlayer byPlayer)
        {
            ItemSlot activeHotbarSlot = byPlayer.InventoryManager.ActiveHotbarSlot;
            if (activeHotbarSlot.Empty)
            {
                return TryTake(byPlayer, InputSlot);
            }
            if (recipeHandler.GetMatchingIngredient(Api.World, activeHotbarSlot, workstationtype, Block.Code.FirstCodePart()))
            {
                return TryPut(byPlayer, activeHotbarSlot, InputSlot);
            }
            updateMeshes();
            MarkDirty(redrawOnClient: true);
            return false;
        }

        public virtual bool OnComplexInteract(IPlayer byPlayer)
        {
            ItemSlot activeHotbarSlot = byPlayer.InventoryManager.ActiveHotbarSlot;
            if (activeHotbarSlot.Empty)
            {
                if (!InputSlot.Empty) return TryTake(byPlayer, InputSlot);
                return TryTake(byPlayer, ProcessModifierSlot);
            }
            if (recipeHandler.GetMatchingIngredient(Api.World, activeHotbarSlot, workstationtype, Block.Code.FirstCodePart()))
            {
                return TryPut(byPlayer, activeHotbarSlot, InputSlot);
            }
            if (recipeHandler.GetMatchingProcessModifier(Api.World, activeHotbarSlot, workstationtype))
            {
                return TryPut(byPlayer, activeHotbarSlot, ProcessModifierSlot);
            }
            updateMeshes();
            MarkDirty(redrawOnClient: true);
            return false;
        }

        public virtual bool TryPut(IPlayer byPlayer, ItemSlot slot, ItemSlot targetSlot)
        {
            // VS 1.22: removed targetSlot != null check (null guard not needed here), TryPutInto no longer takes quantity
            if (targetSlot.Empty)
            {
                Block block = slot.Itemstack.Block;
                if (slot.TryPutInto(Api.World, targetSlot) > 0)
                {
                    // VS 1.22: PlaySoundAt parameter cleanup - removed trailing pitch/range params
                    AssetLocation soundLoc = block?.Sounds?.Place.Location ?? new AssetLocation("sounds/player/build");
                    Api.World.PlaySoundAt(soundLoc, byPlayer.Entity, byPlayer, randomizePitch: true, 16f);
                }
                updateMeshes();
                MarkDirty(redrawOnClient: true);
            }
            return false;
        }

        public virtual bool TryTake(IPlayer byPlayer, ItemSlot targetSlot)
        {
            if (!targetSlot.Empty)
            {
                ItemStack itemStack = targetSlot.TakeOut(1);
                // VS 1.22: TryGiveItemstack no longer takes a bool parameter
                if (byPlayer.InventoryManager.TryGiveItemstack(itemStack))
                {
                    AssetLocation soundLoc = itemStack.Block?.Sounds?.Place.Location ?? new AssetLocation("sounds/player/build");
                    Api.World.PlaySoundAt(soundLoc, byPlayer.Entity, byPlayer, randomizePitch: true, 16f);
                    recipeHandler.clearRecipe();
                }
                if (itemStack.StackSize > 0)
                {
                    Api.World.SpawnItemEntity(itemStack, Pos.ToVec3d().Add(0.5, 0.5, 0.5));
                }
                // VS 1.22: MarkDirty no longer takes a null second param
                base.MarkDirty(redrawOnClient: true);
                return false;
            }
            return false;
        }

        internal bool handleRecipe(CollectibleObject heldCollectible, float secondsUsed, IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel)
        {
            ItemSlot slot = byPlayer.InventoryManager.ActiveHotbarSlot;
            recipecomplete = recipeHandler.processRecipe(heldCollectible, slot, byPlayer, blockSel.Position, this, secondsUsed);

            // VS 1.22: WeatherSystemBase call kept but result discarded (wind speed no longer used for particle velocity here)
            Api.ModLoader.GetModSystem<WeatherSystemBase>()?.WeatherDataSlowAccess.GetWindSpeed(byPlayer.Entity.Pos.XYZ);

            if (recipecomplete) recipeHandler.clearRecipe();
            updateMeshes();
            base.MarkDirty(redrawOnClient: true);
            return !recipecomplete;
        }

        private string GetWorkStationType()
        {
            JsonObject attributes = Block.Attributes["workstationproperties"];
            if (attributes.Exists && attributes["workstationtype"].Exists)
            {
                return attributes["workstationtype"].ToString();
            }
            if (Api.Side.IsClient())
            {
                capi.Logger.Debug(Lang.GetMatching("WorkstationTypeNotDesignated", Block.Class));
            }
            return null;
        }

        protected override float[][] genTransformationMatrices()
        {
            float[][] tfMatrices = new float[Inventory.Count][];
            for (int index = 0; index < Inventory.Count; index++)
            {
                ItemSlot itemSlot = Inventory[index];
                if (itemSlot == null) continue;
                ItemStack itemstack = itemSlot.Itemstack;
                if (itemstack == null) continue;

                if (index == Block.Attributes["workstationproperties"]["slottypes"]["inputslot"].AsInt())
                {
                    string blocktype = "specialadjust" + Block.Code.FirstCodePart().ToString();
                    Matrixf matrix = new Matrixf();
                    if (itemstack.Block != null && itemstack.Block.Attributes[blocktype].Exists)
                    {
                        JsonObject specialadjust = itemstack.Collectible.Attributes[blocktype];
                        switch (Block.Variant["side"])
                        {
                            case "east":
                                matrix.Translate(specialadjust["east"].AsFloat(), 0, 0);
                                matrix.Translate(specialadjust["eastrotateX"].AsFloat(), specialadjust["eastrotateY"].AsFloat(), specialadjust["eastrotateZ"].AsFloat());
                                break;
                            case "west":
                                matrix.Translate(specialadjust["west"].AsFloat(), 0, 0);
                                matrix.Translate(specialadjust["westrotateX"].AsFloat(), specialadjust["westrotateY"].AsFloat(), specialadjust["westrotateZ"].AsFloat());
                                break;
                            case "north":
                                matrix.Translate(0, 0, specialadjust["north"].AsFloat());
                                matrix.Translate(specialadjust["northrotateX"].AsFloat(), specialadjust["northrotateY"].AsFloat(), specialadjust["northrotateZ"].AsFloat());
                                break;
                            case "south":
                                matrix.Translate(0, 0, specialadjust["south"].AsFloat());
                                matrix.Translate(specialadjust["southrotateX"].AsFloat(), specialadjust["southrotateY"].AsFloat(), specialadjust["southrotateZ"].AsFloat());
                                break;
                        }
                    }
                    tfMatrices[index] = matrix.Translate(0, 0.0, 0).Translate(0.5, 0.5, 0.5)
                        .RotateYDeg(Block.Shape.rotateY)
                        .Translate(-0.5, -0.5, -0.5).Values;
                }
                else
                {
                    tfMatrices[index] = new Matrixf().Translate(0.5, 0.5, 0.5).RotateYDeg(Block.Shape.rotateY).Translate(-0.5, -0.5, -0.5).Values;
                }
            }
            return tfMatrices;
        }

        public override void ToTreeAttributes(ITreeAttribute tree)
        {
            if (recipeHandler != null)
            {
                // VS 1.22: curtMode can be null, use null-coalescing to avoid NRE
                tree.SetString("currenttoolmode", recipeHandler.curtMode ?? "");
                tree.SetFloat("lastsecondsused", recipeHandler.lastSecondsUsed);
                tree.SetFloat("recipeprogress", recipeHandler.recipeProgress);
                tree.SetFloat("playnextsound", recipeHandler.playNextSound);
                tree.SetFloat("currentminingdamage", recipeHandler.currentMiningDamage);
                tree.SetFloat("curdmgfromminingspeed", recipeHandler.curDmgFromMiningSpeed);
                tree.SetFloat("totalsecondsused", recipeHandler.totalSecondsUsed);
                tree.SetBool("recipecomplete", recipecomplete);
            }
            base.ToTreeAttributes(tree);
            // Removed: spurious updateMeshes()/MarkDirty() calls that don't belong in serialization
        }

        public override void FromTreeAttributes(ITreeAttribute tree, IWorldAccessor worldForResolving)
        {
            base.FromTreeAttributes(tree, worldForResolving);
            if (recipeHandler == null) recipeHandler = new RecipeHandler(Api, this);
            if (recipeHandler != null)
            {
                recipeHandler.curtMode = tree.GetString("currenttoolmode");
                recipeHandler.lastSecondsUsed = tree.GetFloat("lastsecondsused");
                recipeHandler.recipeProgress = tree.GetFloat("recipeprogress");
                recipeHandler.currentMiningDamage = tree.GetFloat("currentminingdamage");
                recipeHandler.curDmgFromMiningSpeed = tree.GetFloat("curdmgfromminingspeed");
                recipeHandler.totalSecondsUsed = tree.GetFloat("totalsecondsused");
                recipeHandler.playNextSound = tree.GetFloat("playnextsound");
                recipecomplete = tree.GetBool("recipecomplete");
                recipeHandler.api = Api;

                // Restore the active recipe from saved tool mode so it survives chunk unload/reload
                string curtMode = recipeHandler.curtMode;
                if (curtMode != null && Api != null && Block != null && !InputSlot.Empty)
                {
                    recipeHandler.GetMatchingRecipes(Api.World, InputSlot, curtMode,
                        Block.Attributes["inventoryclass"].ToString(),
                        Block.Attributes["workstationproperties"]["workstationtype"].ToString(),
                        out var recipe);
                    if (recipe != null) recipeHandler.recipe = recipe;
                }
            }
            updateMeshes();
        }

        public override void GetBlockInfo(IPlayer forPlayer, StringBuilder dsc)
        {
            var primary = Block.Variant["primary"];
            var secondary = Block.Variant["secondary"];
            var materials = Lang.Get("material-" + $"{primary}") + (secondary != null ? " and " + Lang.Get("material-" + $"{secondary}") : "");
            ItemStack stack = forPlayer.InventoryManager.ActiveHotbarSlot.Itemstack;
            string curToolMode = stack?.Collectible.GetBehavior<BehaviorIDGTool>()?.GetToolModeName(stack).ToString();
            dsc.AppendLine(Lang.GetMatching("indappledgroves:workstationholding") + ": " + (InputSlot.Empty ? Lang.GetMatching("indappledgroves:Empty") : InputSlot.Itemstack.Collectible.GetHeldItemName(InputSlot.Itemstack)));

            if (workstationtype == "complex" && !ProcessModifierSlot.Empty)
            {
                dsc.AppendLine(Lang.GetMatching(processmodifier != null ? processmodifier : "indappledgroves:defaultprocessmodifier") + ProcessModifierSlot.Itemstack.GetName());
                dsc.AppendLine(Lang.GetMatching("indappledgroves:remainingdurability") + ": " + ProcessModifierSlot.Itemstack.Attributes["durability"]);
            }

            string curTMode = forPlayer.InventoryManager.ActiveHotbarSlot.Itemstack?.Collectible.GetBehavior<BehaviorIDGTool>()?.GetToolModeName(forPlayer.InventoryManager?.ActiveHotbarSlot.Itemstack);
            recipeHandler.GetMatchingRecipes(forPlayer.Entity.Api.World, InputSlot, curTMode, Block.Attributes["inventoryclass"].ToString(), Block.Attributes["workstationproperties"]["workstationtype"].ToString(), out WorkstationRecipe retrRecipe);

            if (retrRecipe != null)
            {
                for (int i = 0; i < retrRecipe.Output.Length; i++)
                {
                    // VS 1.22: ResolvedItemStack is now uppercase S
                    ItemStack resolvedItemStack = retrRecipe.Output[i].ResolvedItemStack;
                    dsc.AppendLine(Lang.GetMatching("indappledgroves:recipeoutputstack") + " " + resolvedItemStack.StackSize + " " + resolvedItemStack.Collectible.GetHeldItemName(resolvedItemStack));
                }
                ItemStack resolvedReturnStack = retrRecipe.ReturnStack.ResolvedItemStack ?? null;
                if (resolvedReturnStack.Id != 0)
                {
                    dsc.AppendLine("& " + resolvedReturnStack.StackSize + " " + resolvedReturnStack.Collectible.GetHeldItemName(resolvedReturnStack));
                }
                if (recipeHandler.recipe != null)
                {
                    dsc.AppendLine(Lang.GetMatching("indappledgroves:recipeprogress") + " " + Math.Round((recipeHandler.recipeProgress) * 100) + "%");
                }
            }
            if (ClientSettings.ExtendedDebugInfo)
            {
                dsc.AppendLine(string.Format($"{materials}"));
                dsc.AppendLine(Lang.GetMatching("indappledgroves:attributetransformcode") + ": " + AttributeTransformCode);
                dsc.AppendLine(Lang.GetMatching("indappledgroves:inventoryclassname") + ": " + InventoryClassName);
                dsc.AppendLine(Lang.GetMatching("indappledgroves:currenttoolmode") + ": " + curToolMode);
                if (recipeHandler.recipe != null)
                {
                    dsc.AppendLine(Lang.GetMatching("indappledgroves:recipetoolmode") + ": " + recipeHandler.recipe.ToolMode.ToString());
                    dsc.AppendLine("indappledgroves:recipeworkstation" + ": " + recipeHandler.recipe.RequiredWorkstation.ToString());
                }
            }
        }
    }
}
