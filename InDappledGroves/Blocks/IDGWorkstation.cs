using InDappledGroves.BlockEntities;
using InDappledGroves.CollectibleBehaviors;
using System;
using System.Globalization;
using Vintagestory.API.Common;
using Vintagestory.API.Config;

namespace InDappledGroves.Blocks
{
    internal class IDGWorkstation : Block
    {
        private float playNextSound;
        private float resistance;
        private float lastSecondsUsed;
        private float curDmgFromMiningSpeed;

        public override bool OnBlockInteractStart(IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel)
        {
            // Use blockSel.Position; the old instance field was not thread-safe
            if (!(world.BlockAccessor.GetBlockEntity(blockSel.Position) is IDGBEWorkstation))
            {
                return base.OnBlockInteractStart(world, byPlayer, blockSel);
            }
            return true;
        }

        public override bool OnBlockInteractStep(float secondsUsed, IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel)
        {
            bool result = false;
            // Look up the block entity locally each call; removed unsafe instance field
            if (blockSel != null && world.BlockAccessor.GetBlockEntity(blockSel.Position) is IDGBEWorkstation beworkstation)
            {
                CollectibleObject heldCollectible = byPlayer.InventoryManager.ActiveHotbarSlot.Itemstack?.Collectible;
                if (heldCollectible == null || !heldCollectible.HasBehavior<BehaviorIDGTool>())
                {
                    result = beworkstation.OnInteract(byPlayer);
                }
                else if (!beworkstation.InputSlot.Empty && heldCollectible != null && heldCollectible.HasBehavior<BehaviorIDGTool>())
                {
                    result = beworkstation.handleRecipe(heldCollectible, secondsUsed, world, byPlayer, blockSel);
                }
                beworkstation.updateMeshes();
                beworkstation.MarkDirty(redrawOnClient: true);
            }
            return result;
        }

        public override void OnBlockInteractStop(float secondsUsed, IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel)
        {
            if (blockSel != null && world.BlockAccessor.GetBlockEntity(blockSel.Position) is IDGBEWorkstation beworkstation)
            {
                beworkstation.recipeHandler.playNextSound = 0.5f;
                if (beworkstation.recipeHandler.recipe != null)
                {
                    byPlayer.Entity.StopAnimation(beworkstation.recipeHandler.recipe.Animation);
                }
                if (beworkstation.recipecomplete) beworkstation.recipeHandler.clearRecipe();
                beworkstation.MarkDirty(redrawOnClient: true);
                beworkstation.updateMeshes();
            }
        }

        public override bool OnBlockInteractCancel(float secondsUsed, IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel, EnumItemUseCancelReason cancelReason)
        {
            if (blockSel != null && world.BlockAccessor.GetBlockEntity(blockSel.Position) is IDGBEWorkstation beworkstation && beworkstation.recipeHandler.recipe != null)
            {
                byPlayer.Entity.StopAnimation(beworkstation.recipeHandler.recipe.Animation);
            }
            return base.OnBlockInteractCancel(secondsUsed, world, byPlayer, blockSel, cancelReason);
        }

        public override string GetHeldItemName(ItemStack stack)
        {
            base.GetHeldItemName(stack);
            string primary = Variant["primary"];
            string secondary = Variant["secondary"];
            string materials = Lang.Get("material-" + primary) + (secondary != null ? " and " + Lang.Get("material-" + secondary) : "");
            string blockid = Lang.HasTranslation("indappledgroves:block-" + FirstCodePart()) ? "indappledgroves:block-" + FirstCodePart() : Code.Domain + ":block-" + FirstCodePart();
            return string.Format(CultureInfo.CurrentCulture.TextInfo.ToTitleCase(materials.ToLower()) + " " + Lang.GetMatching(blockid), Array.Empty<object>());
        }
    }
}
