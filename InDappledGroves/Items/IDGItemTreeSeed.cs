using InDappledGroves.Util.Config;
using System.Collections.Generic;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;
using Vintagestory.API.Util;
using Vintagestory.GameContent;

namespace InDappledGroves.Items
{
    public class IDGTreeSeed : ItemTreeSeed
    {
        private WorldInteraction[] interactions;
        private bool isMapleSeed;

        public override void OnLoaded(ICoreAPI api)
        {
            isMapleSeed = Variant["type"] == "maple" || Variant["type"] == "crimsonkingmaple";
            if (api.Side != EnumAppSide.Client) return;

            interactions = ObjectCacheUtil.GetOrCreate<WorldInteraction[]>(api, "treeSeedInteractions", delegate
            {
                List<ItemStack> stacks = new List<ItemStack>();
                foreach (Block block in api.World.Blocks)
                {
                    if (!(block.Code == null) && block.EntityClass != null && block.Fertility > 0)
                    {
                        stacks.Add(new ItemStack(block));
                    }
                }
                return new WorldInteraction[]
                {
                    new WorldInteraction
                    {
                        ActionLangCode = "heldhelp-plant",
                        MouseButton = EnumMouseButton.Right,
                        HotKeyCode = "shift",
                        Itemstacks = stacks.ToArray()
                    }
                };
            });
        }

        public override void OnHeldInteractStart(ItemSlot itemslot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel, bool firstEvent, ref EnumHandHandling handHandling)
        {
            if (blockSel == null || !byEntity.Controls.ShiftKey)
            {
                base.OnHeldInteractStart(itemslot, byEntity, blockSel, entitySel, firstEvent, ref handHandling);
                return;
            }

            if (IDGTreeConfig.Current.SaplingSpacingEnabled)
            {
                bool foundSapling = false;
                BlockPos checkPos = blockSel.Position.UpCopy();
                byEntity.Api.World.BlockAccessor.WalkBlocks(
                    checkPos.AddCopy(-IDGTreeConfig.Current.MinHorizontalSaplingDistance, -IDGTreeConfig.Current.MinVerticalSaplingDistance, IDGTreeConfig.Current.MinHorizontalSaplingDistance),
                    checkPos.AddCopy(IDGTreeConfig.Current.MinHorizontalSaplingDistance, IDGTreeConfig.Current.MinVerticalSaplingDistance, -IDGTreeConfig.Current.MinHorizontalSaplingDistance),
                    delegate(Block block, int x, int y, int z)
                    {
                        if (block.Code.FirstCodePart() == "sapling" || (block.FirstCodePart() == "log" && block.FirstCodePart(1) == "grown"))
                        {
                            foundSapling = true;
                        }
                    });

                if (foundSapling)
                {
                    if (api is ICoreClientAPI capi)
                    {
                        capi.TriggerIngameError("ItemTreeSapling", "tooCloseToGrownTreeOrSapling", "Cannot Plant So Close To Another Tree or Sapling.");
                    }
                    handHandling = EnumHandHandling.NotHandled;
                    return;
                }
            }

            string treetype = Variant["type"];
            Block saplBlock = byEntity.World.GetBlock(AssetLocation.Create("sapling-" + treetype + "-free", Code.Domain));
            if (saplBlock == null) return;

            IPlayer byPlayer = null;
            if (byEntity is EntityPlayer)
            {
                byPlayer = byEntity.World.PlayerByUid(((EntityPlayer)byEntity).PlayerUID);
            }

            blockSel = blockSel.Clone();
            blockSel.Position.Up();
            string failureCode = "";
            if (!saplBlock.TryPlaceBlock(api.World, byPlayer, itemslot.Itemstack, blockSel, ref failureCode))
            {
                // PlaySoundAt no longer takes null/range/volume params in VS 1.22
                if (api is ICoreClientAPI capi2 && failureCode != null && failureCode != "__ignore__")
                {
                    capi2.TriggerIngameError(this, failureCode, Lang.Get("placefailure-" + failureCode));
                }
                return;
            }

            byEntity.World.PlaySoundAt(new AssetLocation("sounds/block/dirt1"), (float)blockSel.Position.X + 0.5f, blockSel.Position.Y, (float)blockSel.Position.Z + 0.5f, byPlayer);

            if (((byEntity is EntityPlayer ep) ? ep.Player : null) is IClientPlayer clientPlayer)
            {
                clientPlayer.TriggerFpAnimation(EnumHandInteract.HeldItemInteract);
            }

            // Only consume the seed outside creative mode
            if (byPlayer == null || byPlayer.WorldData?.CurrentGameMode != EnumGameMode.Creative)
            {
                itemslot.TakeOut(1);
                itemslot.MarkDirty();
            }

            handHandling = EnumHandHandling.PreventDefault;
        }

        public override WorldInteraction[] GetHeldInteractionHelp(ItemSlot inSlot)
        {
            return interactions.Append(base.GetHeldInteractionHelp(inSlot));
        }
    }
}
