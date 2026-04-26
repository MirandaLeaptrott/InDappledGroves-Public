using InDappledGroves.BlockEntities;
using InDappledGroves.CollectibleBehaviors;
using System.Collections.Generic;
using Vintagestory.API.Common;
using static InDappledGroves.Util.RecipeTools.IDGRecipeNames;
using Vintagestory.API.MathTools;
using InDappledGroves.Util.Config;
using Vintagestory.API.Client;
using Vintagestory.API.Config;
using Vintagestory.API.Common.Entities;
using Vintagestory.GameContent;
using System;

namespace InDappledGroves.Util.Handlers
{
    public class RecipeHandler
    {
        public RecipeValues recipeValues;
        public float currentMiningDamage { get; set; }

        public float recipeProgress { get; set; }

        public float playNextSound { get; set; }
        public float resistance { get; set; }
        public float lastSecondsUsed { get; set; }

        public float totalSecondsUsed { get; set; }
        public float curDmgFromMiningSpeed { get; set; }

        public WorkstationRecipe recipe { get; set; }

        public string curtMode { get; set; }

        public float toolModeMod { get; set; }

        public ICoreAPI api;

        public IDGBEWorkstation beworkstation { get; set; }

        //public string curTMode { get; set; }

        // Properties instead of fields; field assignment captured an empty list before recipes loaded
        internal static List<BasicWorkstationRecipe> bwsRecipes => IDGRecipeRegistry.Loaded.BasicWorkstationRecipes;

        internal static List<ComplexWorkstationRecipe> cwsRecipes => IDGRecipeRegistry.Loaded.ComplexWorkstationRecipes;

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

        public RecipeHandler(ICoreAPI Api, IDGBEWorkstation beworkstation)
        {
            this.api = Api;
            this.beworkstation = beworkstation;
            RecipeHandler.dustParticles.ParticleModel = EnumParticleModel.Quad;
            RecipeHandler.dustParticles.AddPos.Set(1.0, 1.0, 1.0);
            RecipeHandler.dustParticles.MinQuantity = 2f;
            RecipeHandler.dustParticles.AddQuantity = 12f;
            RecipeHandler.dustParticles.LifeLength = 4f;
            RecipeHandler.dustParticles.MinSize = 0.2f;
            RecipeHandler.dustParticles.MaxSize = 0.5f;
            RecipeHandler.dustParticles.MinVelocity.Set(-0.4f, -0.4f, -0.4f);
            RecipeHandler.dustParticles.AddVelocity.Set(0.8f, 1.2f, 0.8f);
            RecipeHandler.dustParticles.DieOnRainHeightmap = false;
            RecipeHandler.dustParticles.WindAffectednes = 0.25f;
        }

        public void clearRecipe(bool clearCurrentMiningDamage = true)
        {
            recipe = null;
            recipeProgress = 0;
            recipeValues = null;
            playNextSound = .5f;
            resistance = 0;
            lastSecondsUsed = 0;
            curDmgFromMiningSpeed = 0;
            toolModeMod = 0;
            currentMiningDamage = 0;
            totalSecondsUsed = 0;
            beworkstation.MarkDirty(true);
        }



        public bool GetMatchingIngredient(IWorldAccessor world, ItemSlot slot, string workstationtype, string requiredworkstation)
        {
            string processmodifiercheck = slot.Itemstack.Collectible.FirstCodePart() + "-" + slot.Itemstack.Collectible.FirstCodePart(1);
            if (workstationtype == "basic")
            {
                if (bwsRecipes != null)
                {
                    for (int j = 0; j < bwsRecipes.Count; j++)
                    {
                        if (bwsRecipes[j].RequiredWorkstation == requiredworkstation && bwsRecipes[j].Matches(world, slot))
                        {
                            return true;
                        }
                    }
                }
                return false;
            }
            else if (workstationtype == "complex")
            {
                for (int j = 0; j < cwsRecipes.Count; j++)
                {
                    //TODO: This needs to be setup to accommodate recipes that only work for one processmodifier
                    if (!beworkstation.ProcessModifierSlot.Empty && cwsRecipes[j].RequiredWorkstation == requiredworkstation && cwsRecipes[j].Matches(world, slot))
                    {
                        //Checks to see if player is holding a valid ingredient or valid processmodifier
                        return true;
                    }
                }
            }

            return false;
        }

        public bool GetMatchingProcessModifier(IWorldAccessor world, ItemSlot slot, string workstationtype)
        {
            string processmodifiercheck = slot.Itemstack.Collectible.FirstCodePart() + "-" + slot.Itemstack.Collectible.FirstCodePart(1);

            for (int j = 0; j < cwsRecipes.Count; j++)
            {
                if (cwsRecipes[j].ProcessModifier == processmodifiercheck)
                {
                    //Checks to see if player is holding a valid ingredient or valid processmodifier
                    return true;
                }
            }
            return false;
        }

        public virtual bool GetMatchingRecipes(IWorldAccessor world, ItemSlot slots, string curTMode, string workstationname, string workstationtype, out WorkstationRecipe recipe)
        {

            recipe = null;
            if (workstationname == null || workstationtype == null) return false;

            if (workstationtype == "basic")
            {
                for (int j = 0; j < bwsRecipes.Count; j++)
                {
                    if (bwsRecipes[j].Matches(world, slots) && bwsRecipes[j].RequiredWorkstation == workstationname && (bwsRecipes[j].ToolMode == curTMode || curTMode == "any"))
                    {
                        recipe = bwsRecipes[j];
                        return true;
                    }
                }
            }
            else if (workstationtype == "complex")
            {
                if (curTMode == null) return false;
                for (int j = 0; j < cwsRecipes.Count; j++)
                {
                    string processmodifier = beworkstation.ProcessModifierSlot?.Itemstack?.Collectible.FirstCodePart() + "-" + beworkstation.ProcessModifierSlot?.Itemstack?.Collectible.FirstCodePart(1);
                    if (cwsRecipes[j].Matches(world, slots) && (cwsRecipes[j].RequiredWorkstation == workstationname && (cwsRecipes[j].ToolMode == curTMode || curTMode == "any") && cwsRecipes[j].ProcessModifier == processmodifier))
                    {
                        recipe = cwsRecipes[j];
                        return true;
                    }
                }
            }

            return false;
        }

        public virtual bool GetMatchingRecipes(IWorldAccessor world, ItemStack stack, string curTMode, string workstationname, string workstationtype, out WorkstationRecipe recipe)
        {
            DummySlot slots = new DummySlot();
            slots.Itemstack = stack;
            recipe = null;
            if (workstationname == null || workstationtype == null) return false;

            if (workstationtype == "basic")
            {
                for (int j = 0; j < bwsRecipes.Count; j++)
                {
                    if (bwsRecipes[j].Matches(world, slots) && bwsRecipes[j].RequiredWorkstation == workstationname && (bwsRecipes[j].ToolMode == curTMode || curTMode == "any"))
                    {
                        recipe = bwsRecipes[j];
                        return true;
                    }
                }
            }
            else if (workstationtype == "complex")
            {
                if (curTMode == null) return false;
                for (int j = 0; j < cwsRecipes.Count; j++)
                {
                    string processmodifier = beworkstation.ProcessModifierSlot?.Itemstack?.Collectible.FirstCodePart() + "-" + beworkstation.ProcessModifierSlot?.Itemstack?.Collectible.FirstCodePart(1);
                    if (cwsRecipes[j].Matches(world, slots) && (cwsRecipes[j].RequiredWorkstation == workstationname && (cwsRecipes[j].ToolMode == curTMode || curTMode == "any") && cwsRecipes[j].ProcessModifier == processmodifier))
                    {
                        recipe = cwsRecipes[j];
                        return true;
                    }
                }
            }

            return false;
        }

        public bool processRecipe(CollectibleObject heldCollectible, ItemSlot activehotbarslot, IPlayer player, BlockPos pos, IDGBEWorkstation beworkstation, float secondsUsed)
        {
            string curTMode = heldCollectible.GetBehavior<BehaviorIDGTool>().GetToolModeName(player.InventoryManager.ActiveHotbarSlot.Itemstack);
            string workstationtype = beworkstation.Block.Attributes["workstationproperties"]["workstationtype"].ToString();
            float curMiningSpeed = 0;
            if (recipe != null && recipe.ToolMode != curTMode)
            {
                clearRecipe();
                return false;
            }
            if (recipe == null)
            {
                WorkstationRecipe retrRecipe;

                GetMatchingRecipes(player.Entity.Api.World, beworkstation.InputSlot, curTMode, beworkstation.Block.Attributes["inventoryclass"].ToString(), beworkstation.Block.Attributes["workstationproperties"]["workstationtype"].ToString(), out retrRecipe);
                if (retrRecipe == null)
                {
                    return false;
                }

                recipe = retrRecipe;
            }

            if (recipeValues == null) GetRecipeValues(workstationtype, beworkstation.InputSlot.Itemstack, recipe);

            toolModeMod = heldCollectible.GetBehavior<BehaviorIDGTool>().GetToolModeMod(activehotbarslot.Itemstack);
            EntityPlayer entityPlayer = player.Entity;
            entityPlayer.StartAnimation(recipe.Animation);

            if (player.Entity.Api.Side == EnumAppSide.Server)
            {
                //TODO Put Recipe And Resistance Getting To A Separate Method
                ItemStack InputStack = recipeValues.InputStack;

                resistance = (InputStack.Block is Block ? InputStack.Block.Resistance
                : InputStack.Item.Attributes["resistance"].AsFloat()) * IDGToolConfig.Current.baseWorkstationResistanceMult;
                if (playNextSound < secondsUsed)
                {
                    player.Entity.Api.World.PlaySoundAt(new AssetLocation(recipe.Sound), beworkstation.Pos.X, beworkstation.Pos.Y, beworkstation.Pos.Z);
                    playNextSound += 1.5f;
                }
                lastSecondsUsed = secondsUsed - lastSecondsUsed < 0 ? 0 : lastSecondsUsed;
                curMiningSpeed = GetCurMiningSpeed(InputStack, heldCollectible, player);
                curDmgFromMiningSpeed = (curMiningSpeed * toolModeMod) * (1 + IDGToolConfig.Current.baseWorkstationMiningSpdMult);
                float curSecondsUsed = secondsUsed - lastSecondsUsed < 0 ? 0 : secondsUsed - lastSecondsUsed;
                currentMiningDamage += curSecondsUsed * curDmgFromMiningSpeed;
                lastSecondsUsed = secondsUsed;
                this.recipeProgress = currentMiningDamage / resistance;

                if (currentMiningDamage >= resistance && secondsUsed > 0.25f)
                {
                    if (beworkstation.workstationtype == "complex")
                    {
                        if (beworkstation.Block.Attributes["workstationproperties"]["damageprocessmodifier"].AsBool() == true)
                        {
                            beworkstation.ProcessModifierSlot.Itemstack.Collectible.DamageItem(player.Entity.Api.World, player.Entity, beworkstation.ProcessModifierSlot, 1);
                        }
                    }
                    heldCollectible.DamageItem(player.Entity.Api.World, entityPlayer, entityPlayer.RightHandItemSlot, recipeValues.baseToolDamage);
                    CompleteRecipe(api, player);
                    beworkstation.MarkDirty(true);
                    return true;
                }
            }
            WeatherSystemBase modSystem = player.Entity.World.Api.ModLoader.GetModSystem<WeatherSystemBase>(true);
            double windspeed = (modSystem != null) ? modSystem.WeatherDataSlowAccess.GetWindSpeed(player.Entity.Pos.XYZ) : 0.0;
            ItemStack sourceStack = beworkstation.InputSlot.Itemstack;
            if (player.Entity.Api.World.Side == EnumAppSide.Client)
            {
                RecipeHandler.dustParticles.Color = sourceStack.Collectible.GetRandomColor(player.Entity.World.Api as ICoreClientAPI, sourceStack);
                RecipeHandler.dustParticles.Color |= -16777216;
                RecipeHandler.dustParticles.MinPos.Set((double)beworkstation.Pos.X, (double)beworkstation.Pos.Y, (double)beworkstation.Pos.Z);
                RecipeHandler.dustParticles.Pos.Set((double)beworkstation.Pos.X, (double)beworkstation.Pos.Y + 2, (double)beworkstation.Pos.Z);
                RecipeHandler.dustParticles.MinQuantity = 1f;
                RecipeHandler.dustParticles.AddQuantity = 4f;
                RecipeHandler.dustParticles.GravityEffect = 0.8f;
                RecipeHandler.dustParticles.ParticleModel = EnumParticleModel.Cube;
                RecipeHandler.dustParticles.MinVelocity.Set(-0.4f + (float)windspeed, -0.4f, -0.4f);
                RecipeHandler.dustParticles.AddVelocity.Set(0.8f + (float)windspeed, 1.2f, 0.8f);
                player.Entity.World.SpawnParticles(RecipeHandler.dustParticles, null);
            }

            beworkstation.MarkDirty(true);
            return false;
        }

        private float GetCurMiningSpeed(ItemStack inputStack, CollectibleObject heldCollectible, IPlayer player)
        {
            if (inputStack.Collectible is Block && player.Entity.BlockSelection != null)
            {
                return (heldCollectible.GetMiningSpeed(player.Entity.ActiveHandItemSlot.Itemstack, player.Entity.BlockSelection, inputStack.Block, player as IPlayer));
            }
            else
            {
                return (heldCollectible.MiningSpeed[(EnumBlockMaterial)recipeValues.ingredientMaterial]);
            }
        }

        private void GetRecipeValues(string workstationtype, ItemStack itemstack, WorkstationRecipe recipe)
        {
            if (workstationtype == "basic")
            {
                // Fix applied here for single workstation recipe output handling
                recipeValues = new RecipeValues(beworkstation.InputSlot.Itemstack, recipe.IngredientMaterial, recipe.Output.ResolvedItemStack, recipe.ReturnStack.ResolvedItemStack, recipe.BaseToolDmg, null);
            }
            else if (workstationtype == "complex")
            {
                string processmodifier = beworkstation.ProcessModifierSlot.Itemstack.Collectible.FirstCodePart() + "-" + beworkstation.ProcessModifierSlot.Itemstack.Collectible.FirstCodePart(1);
                recipeValues = new RecipeValues(beworkstation.InputSlot.Itemstack, recipe.IngredientMaterial, recipe.Output.ResolvedItemStack, recipe.ReturnStack.ResolvedItemStack, recipe.BaseToolDmg, processmodifier);
            }
        }

        public bool CompleteRecipe(ICoreAPI api, IPlayer byPlayer)
        {
            ItemStack returnStack = recipe.ReturnStack.ResolvedItemStack;
            if (returnStack.Collectible.FirstCodePart() == "air")
            {
                if (beworkstation.InputSlot.Empty) return false;
                beworkstation.InputSlot.Itemstack = null;
                byPlayer.Entity.StopAnimation(recipe.Animation);
                SpawnOutput(recipeValues.output, byPlayer.Entity, byPlayer.Entity.BlockSelection.Position);
                clearRecipe();
                return false; //If no stack is returned, clear stack
            }
            else
            {
                //TODO: Determine if check needed to prevent spawning of excess resources
                beworkstation.InputSlot.Itemstack = null;
                ReturnStackPut(returnStack.Clone(), beworkstation);
                byPlayer.Entity.StopAnimation(recipe.Animation);
                SpawnOutput(recipeValues.output, byPlayer.Entity, byPlayer.Entity.BlockSelection.Position);
                clearRecipe();
                return true; //If a stack is returned from the recipe, allow process to continue after resetting dmg accumulation
            }
        }

        public virtual void ReturnStackPut(ItemStack stack, IDGBEWorkstation workstation)
        {
            if (workstation.InputSlot.Empty)
            {
                workstation.InputSlot.Itemstack = stack;
            }
        }

        public void SpawnOutput(ItemStack output, EntityAgent byEntity, BlockPos pos)
        {
            if (output == null) return;

            int j = output.StackSize;
            if (!byEntity.TryGiveItemStack(new ItemStack(output.Collectible, j)))
            {
                for (int i = j; i > 0; i--)
                {
                    byEntity.World.SpawnItemEntity(new ItemStack(output.Collectible), pos.ToVec3d(), new Vec3d(0.05f, 0.1f, 0.05f));
                }
            }
            clearRecipe();
        }


        public class RecipeValues
        {
            internal ItemStack InputStack;
            internal int ingredientMaterial;
            internal string processmodifier;
            internal ItemStack output;
            internal ItemStack returnStack;
            internal int baseToolDamage;

            public RecipeValues(ItemStack InputStack, int ingredientMaterial, ItemStack output, ItemStack returnStack, int baseToolDamage, string processmodifier = null)
            {
                this.InputStack = InputStack;
                this.ingredientMaterial = ingredientMaterial;
                this.output = output;
                this.processmodifier = processmodifier;
                this.returnStack = returnStack;
                this.baseToolDamage = baseToolDamage;
            }
        }
    }
}
