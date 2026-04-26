using InDappledGroves.Interfaces;
using InDappledGroves.Util.Config;
using InDappledGroves.Util.Handlers;
using System;
using System.Collections.Generic;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.GameContent;
using static InDappledGroves.Util.RecipeTools.IDGRecipeNames;

namespace InDappledGroves.CollectibleBehaviors
{
    
    class BehaviorIDGTool : CollectibleBehavior, IIDGTool
    {
        private SimpleParticleProperties InitializeParticles()
        {
            return new SimpleParticleProperties()
            {
                MinPos = new Vec3d(),
                AddPos = new Vec3d(),
                MinQuantity = 0,
                AddQuantity = 3,
                Color = ColorUtil.ToRgba(100, 200, 200, 200),
                GravityEffect = 1f,
                WithTerrainCollision = true,
                ParticleModel = EnumParticleModel.Quad,
                LifeLength = 0.5f,
                MinVelocity = new Vec3f(-1, 2, -1),
                AddVelocity = new Vec3f(2, 0, 2),
                MinSize = 0.2f,
                MaxSize = 0.5f,
                WindAffected = true
            };
        }

        static readonly SimpleParticleProperties dustParticles = new()
        {
            MinPos = new Vec3d(),
            AddPos = new Vec3d(),
            MinQuantity = 0,
            AddQuantity = 3,
            Color = ColorUtil.ToRgba(100, 200, 200, 200),
            GravityEffect = 1f,
            WithTerrainCollision = true,
            ParticleModel = EnumParticleModel.Quad,
            LifeLength = 0.5f,
            MinVelocity = new Vec3f(-1, 2, -1),
            AddVelocity = new Vec3f(2, 0, 2),
            MinSize = 0.2f,
            MaxSize = 0.5f,
            WindAffected = true
        };

        public override void OnLoaded(ICoreAPI api)
        {
            base.OnLoaded(api);
            this.api = api as ICoreAPI;
            capi = this.api as ICoreClientAPI;
            toolModes = BuildSkillList();
        }

        public BehaviorIDGTool(CollectibleObject collobj) : base(collobj)
        {
            Inventory = new InventoryGeneric(1, "IDGTool-slot", null, null);
            tempInv = new InventoryGeneric(1, "IDGTool-WorldInteract", null, null);
            BehaviorIDGTool.dustParticles.ParticleModel = EnumParticleModel.Quad;
            BehaviorIDGTool.dustParticles.AddPos.Set(1.0, 1.0, 1.0);
            BehaviorIDGTool.dustParticles.MinQuantity = 2f;
            BehaviorIDGTool.dustParticles.AddQuantity = 12f;
            BehaviorIDGTool.dustParticles.LifeLength = 4f;
            BehaviorIDGTool.dustParticles.MinSize = 0.2f;
            BehaviorIDGTool.dustParticles.MaxSize = 0.5f;
            BehaviorIDGTool.dustParticles.MinVelocity.Set(-0.4f, -0.4f, -0.4f);
            BehaviorIDGTool.dustParticles.AddVelocity.Set(0.8f, 1.2f, 0.8f);
            BehaviorIDGTool.dustParticles.DieOnRainHeightmap = false;
            BehaviorIDGTool.dustParticles.WindAffectednes = 0.25f;
        }

        #region ToolMode Stuff
        private SkillItem[] BuildSkillList()
        {
            var skillList = new List<SkillItem>();
            foreach (var behaviour in collObj.CollectibleBehaviors)
            {
                if (behaviour is not IBehaviorVariant bwc) continue;
                foreach (var mode in bwc.GetSkillItems())
                {
                    skillList.Add(mode);
                }
            }
            return skillList.ToArray();
        }

        public override SkillItem[] GetToolModes(ItemSlot slot, IClientPlayer forPlayer, BlockSelection blockSel)
        {

            return BuildSkillList();
        }

        public string GetToolModeName(ItemStack stack)
        {
            toolModes = BuildSkillList();
            return toolModes[Math.Min(toolModes.Length - 1, stack.Attributes.GetInt("toolMode", 0))].Code.FirstCodePart();
        }

        public override int GetToolMode(ItemSlot slot, IPlayer byPlayer, BlockSelection blockSelection)
        {
            int value = slot.Itemstack.Attributes.GetInt("toolMode");
            return value;
        }

        public override void SetToolMode(ItemSlot slot, IPlayer byPlayer, BlockSelection blockSel, int toolMode)
        {
            slot.Itemstack.Attributes.SetInt("toolMode", toolMode);
        }

        public override void OnBeforeRender(ICoreClientAPI capi, ItemStack stack, EnumItemRenderTarget target, ref ItemRenderInfo renderinfo)
        {


            if (stack.Attributes.HasAttribute("toolMode"))
            {
                JsonObject transformAttributes = stack.Collectible.Attributes["modeTransforms"][GetToolModeName(stack)];

                if (target.ToString() == "HandFp")
                {
                    renderinfo.Transform = transformAttributes?["fpHandTransform"].AsObject<ModelTransform>() ?? collObj.FpHandTransform;
                }
                if (target is EnumItemRenderTarget.HandTp or EnumItemRenderTarget.HandTpOff)
                {
                    renderinfo.Transform = transformAttributes?["tpHandTransform"].AsObject<ModelTransform>() ?? collObj.TpHandTransform;
                }
            }
            base.OnBeforeRender(capi, stack, target, ref renderinfo);
        }

        #endregion ToolMode Stuff

        public override void OnHeldInteractStart(ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel, bool firstEvent, ref EnumHandHandling handHandling, ref EnumHandling handling)
        {
            if (byEntity.Controls.CtrlKey || blockSel == null) { handHandling = EnumHandHandling.PreventDefault; return; }

            // Resolve recipe for this block + current tool mode
            Inventory[0].Itemstack = new ItemStack(api.World.BlockAccessor.GetBlock(blockSel.Position, 0));
            string curTMode = GetToolModeName(slot.Itemstack);
            GroundRecipe recipe = GetMatchingGroundRecipe(Inventory[0], curTMode);
            if (recipe == null) return;
            ItemStack stack = slot.Itemstack;
            Block recipeBlock = api.World.BlockAccessor.GetBlock(blockSel.Position, 0);
            float toolModeMod = GetToolModeMod(slot.Itemstack);
            if (toolModeMod <= 0f) toolModeMod = 1f;

            float resistance = recipeBlock.Resistance * IDGToolConfig.Current.baseGroundRecipeResistanceMult;

            // Durability gate (leave on client too if you want UX, but server decides)
            if (slot.Itemstack.Attributes.GetInt("durability") < recipe.BaseToolDmg && slot.Itemstack.Attributes.GetInt("durability") != 0)
            {
                (api as ICoreClientAPI)?.TriggerIngameError(this, "toolittledurability", Lang.Get("indappledgroves:toolittledurability", recipe.BaseToolDmg));
                return;
            }

            // --- write the whole job into the item’s temp attributes ---
            ITreeAttribute w = GetWork(slot.Itemstack);
            w.SetString("anim", recipe.Animation);
            w.SetInt("x", blockSel.Position.X);
            w.SetInt("y", blockSel.Position.Y);
            w.SetInt("z", blockSel.Position.Z);
            w.SetInt("blockId", recipeBlock.Id);
            w.SetFloat("resistance", resistance);
            w.SetFloat("lastUsed", 0f);
            w.SetFloat("totalUsed", 0f);
            w.SetFloat("toolModeMod", toolModeMod);
            w.SetFloat("nextSoundAt", 0.25f);
            w.SetBool("complete", false);
            // optional: store recipe identity needed at complete time
            w.SetString("recipeCode", recipe.Code ?? curTMode);

            // Server starts (replicated) animation once
            byEntity.StartAnimation(w.GetString("anim"));

            handHandling = EnumHandHandling.Handled;
        }

        public override bool OnHeldInteractStep(float secondsUsed, ItemSlot slot, EntityAgent byEntity,
                                        BlockSelection blockSel, EntitySelection entitySel, ref EnumHandling handling)
        {
            if (blockSel == null) return false;

            ITreeAttribute w = slot.Itemstack.TempAttributes.GetTreeAttribute("idgWork");
            if (w == null) return false;

            // Validate target: same pos & same block id (prevents carry-over to other blocks)
            BlockPos target = new BlockPos(w.GetInt("x"), w.GetInt("y"), w.GetInt("z"));

            if (!target.Equals(blockSel.Position)) { byEntity.StopAnimation(w.GetString("anim")); return false; }
            if (api.World.BlockAccessor.GetBlock(target).Id != w.GetInt("blockId")) { 
                byEntity.StopAnimation(w.GetString("anim")); return false; }

            // Advance timers/progress
            float last = w.GetFloat("lastUsed");
            float dt = secondsUsed - last; if (dt < 0f) dt = 0f;
            w.SetFloat("lastUsed", secondsUsed);

            // Use current mining speed (server-authoritative)
            float curSpd = slot.Itemstack.Collectible.GetMiningSpeed(slot.Itemstack, blockSel,
                             api.World.BlockAccessor.GetBlock(target), byEntity as IPlayer);
            float toolModeMod = w.GetFloat("toolModeMod", 1f);
            float dmgPerSecond = curSpd * toolModeMod * IDGToolConfig.Current.baseGroundRecipeMiningSpdMult;

            float total = w.GetFloat("totalUsed") + dt;
            w.SetFloat("totalUsed", total);

            float resistance = w.GetFloat("resistance");
            float curDamage = total * dmgPerSecond;

            // Server: sounds and completion
            if (api.Side == EnumAppSide.Server)
            {
                float nextSoundAt = w.GetFloat("nextSoundAt", 0.25f);
                if (secondsUsed >= nextSoundAt)
                {
                    // play your recipe sound here (you can store the path in the blob if needed)
                    w.SetFloat("nextSoundAt", nextSoundAt + 0.5f);
                }

                if (curDamage >= resistance && secondsUsed > 0.25f)
                {
                    // Perform SpawnOutput + block swap
                    GroundRecipe recipe = GetMatchingGroundRecipe(Inventory[0], GetToolModeName(slot.Itemstack));
                    if (recipe != null)
                    {
                        SpawnOutput(recipe, target, byEntity);
                        api.World.BlockAccessor.SetBlock(ReturnStackId(recipe, target), target);
                        api.World.BlockAccessor.TriggerNeighbourBlockUpdate(target);
                    }

                    w.SetBool("complete", true);
                    handling = EnumHandling.Handled;
                    return false;
                }
            }

            if (api.Side == EnumAppSide.Client)
            {
                var p = new SimpleParticleProperties
                {
                    MinPos = new Vec3d(blockSel.Position.X, blockSel.Position.Y + 0.25, blockSel.Position.Z),
                    AddPos = new Vec3d(1, 1, 1),
                    MinQuantity = 1,
                    AddQuantity = 4,
                    GravityEffect = 0.8f,
                    ParticleModel = EnumParticleModel.Cube,
                    LifeLength = 0.5f,
                    MinVelocity = new Vec3f(-0.4f, -0.4f, -0.4f),
                    AddVelocity = new Vec3f(0.8f, 1.2f, 0.8f),
                    MinSize = 0.2f,
                    MaxSize = 0.5f,
                    Color = slot.Itemstack.Collectible.GetRandomColor(api as ICoreClientAPI, Inventory[0].Itemstack) | unchecked((int)0xFF000000)
                };
                byEntity.World.SpawnParticles(p, null);
            }

            handling = EnumHandling.Handled;
            return true;
        }

        public override void OnHeldInteractStop(float secondsUsed, ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel, ref EnumHandling handling)
        {
            var w = slot.Itemstack.TempAttributes.GetTreeAttribute("idgWork");
            string anim = w?.GetString("anim") ?? "axechop";

            if (blockSel != null)
            {
                api.World.BlockAccessor.MarkBlockDirty(blockSel.Position);
            }

            if (anim.Length > 0)
            {
                byEntity.StopAnimation(anim);
            }

            // If the job completed, apply base tool damage once
            bool complete = w?.GetBool("complete") == true;
            if (complete)
            {
                // Re-lookup recipe at stop (or store what you need in the blob at Start)
                GroundRecipe recipeAtStop = GetMatchingGroundRecipe(Inventory[0], GetToolModeName(slot.Itemstack));
                if (recipeAtStop != null)
                {
                    slot.Itemstack.Collectible.DamageItem(api.World, byEntity, slot, recipeAtStop.BaseToolDmg);
                }
            }
            ClearWork(slot.Itemstack);
        }

        public override bool OnHeldInteractCancel(float secondsUsed, ItemSlot slot, EntityAgent byEntity,BlockSelection blockSel, EntitySelection entitySel, EnumItemUseCancelReason reason, ref EnumHandling handling)
        {
            ITreeAttribute w = slot.Itemstack.TempAttributes.GetTreeAttribute("idgWork");
            if (w != null)
            {
                byEntity.StopAnimation(w.GetString("anim"));
                ClearWork(slot.Itemstack);
            }
            return base.OnHeldInteractCancel(secondsUsed, slot, byEntity, blockSel, entitySel, reason, ref handling);
        }


        public float GetToolModeMod(ItemStack stack)
        {
            if (api.Side.IsServer())
            {
                String propString = GetToolModeName(stack) + "Props";
                String multString = GetToolModeName(stack) + "Multiplier";
                if (stack.Collectible.Attributes[propString].Exists && stack.Collectible.Attributes[propString][multString].Exists)
                {
                    float modemod = stack.Collectible.Attributes[propString][multString].AsFloat();
                    return modemod;
                }
                return 1f;
            }
            return 1f;

        }

        //-- Spawns output when chopping cycle is finished --//
        private int ReturnStackId(GroundRecipe recipe, BlockPos pos)
        {
            if (recipe.ReturnStack.ResolvedItemStack.Collectible is Block)
            {
                return recipe.ReturnStack.ResolvedItemStack.Id;
            }
            else if (recipe.ReturnStack.ResolvedItemStack.Collectible is Item)
            {
                SpawnReturnstackItem(recipe.ReturnStack.ResolvedItemStack, pos);
                return 0;
            }
            return 0;
        }

        public void SpawnOutput(GroundRecipe recipe, BlockPos pos, EntityAgent byEntity)
        {
            // Fix applied here for single ground recipe output handling
            JsonItemStack stack = recipe.Output;
            if (stack == null || stack.ResolvedItemStack == null)
            {
                return;
            }

            int j = stack.ResolvedItemStack.StackSize;
            if (!byEntity.TryGiveItemStack(new ItemStack(stack.ResolvedItemStack.Collectible, j)))
            {
                for (int i = j; i > 0; i--)
                {
                    byEntity.World.SpawnItemEntity(new ItemStack(stack.ResolvedItemStack.Collectible), pos.ToVec3d(), new Vec3d(0.05f, 0.1f, 0.05f));
                }
            }
        }

        public void SpawnReturnstackItem(ItemStack stack, BlockPos pos)
        {
            // Was using recipe.ReturnStack instead of the passed stack (bug)
            int j = stack.StackSize;
            for (int i = j; i > 0; i--)
            {
                api.World.SpawnItemEntity(new ItemStack(stack.Collectible), pos.ToVec3d(), new Vec3d(0.05f, 0.1f, 0.05f));
            }
        }

        public GroundRecipe GetMatchingGroundRecipe(ItemSlot slot, string curTMode)
        {
            List<GroundRecipe> recipes = IDGRecipeRegistry.Loaded.GroundRecipes;

            if (recipes == null) return null;
            for (int j = 0; j < recipes.Count; j++)
            {
                if (recipes[j].Matches(api.World, slot) && recipes[j].ToolMode == curTMode)
                {
                    return recipes[j];
                }
            }

            return null;
        }

        public bool DoesSlotMatchRecipe(ItemSlot slots)
        {
            List<GroundRecipe> recipes = IDGRecipeRegistry.Loaded.GroundRecipes;
            if (recipes == null) return false;

            for (int j = 0; j < recipes.Count; j++)
            {
                if (recipes[j].Matches(capi.World, slots))
                {
                    return true;
                }
            }

            return false;
        }

        private static ITreeAttribute GetWork(ItemStack stack)
        {
            return stack.TempAttributes.GetOrAddTreeAttribute("idgWork");
        }

        private static void ClearWork(ItemStack stack)
        {
            stack.TempAttributes.RemoveAttribute("idgWork");
        }

        #region Recipe Processing
        public GroundRecipe GetMatchingGroundRecipe(IWorldAccessor world, ItemSlot slot, string curTMode)
        {
            List<GroundRecipe> recipes = IDGRecipeRegistry.Loaded.GroundRecipes;
            if (recipes == null) return null;

            for (int j = 0; j < recipes.Count; j++)
            {
                if (recipes[j].Matches(capi.World, slot) && recipes[j].ToolMode == curTMode)
                {
                    return recipes[j];
                }
            }

            return null;
        }
        #endregion Recipe Processing

        public ICoreAPI api;
        public ICoreClientAPI capi;

        public string InventoryClassName => "worldinventory";
        public InventoryBase Inventory { get; }
        public InventoryBase tempInv { get; }
        public SkillItem[] toolModes;
    }


}
