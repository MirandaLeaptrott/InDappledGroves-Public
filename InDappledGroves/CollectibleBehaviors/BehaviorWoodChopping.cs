using InDappledGroves.CollectibleBehaviors;
using InDappledGroves.Util.Config;
using System;
using System.Collections.Generic;
using System.Linq;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Util;
using Vintagestory.GameContent;

namespace InDappledGroves
{
    class BehaviorWoodChopping : CollectibleBehavior, IBehaviorVariant
    { 
        ICoreAPI api;
        ICoreClientAPI capi;

        public BehaviorWoodChopping(CollectibleObject collObj) : base(collObj)
        {
            this.collObj = collObj;
        }

        
        public SkillItem[] GetSkillItems()
        {
            return toolModes ?? new SkillItem[] { null };
        }

        public override void OnLoaded(ICoreAPI api)
        {
            this.api = api;
            this.capi = (api as ICoreClientAPI);

            this.toolModes = ObjectCacheUtil.GetOrCreate<SkillItem[]>(api, "idgAxeChopModes", delegate
            {

                SkillItem[] array;
                array = new SkillItem[]
                {
                        new SkillItem
                        {
                            Code = new AssetLocation("chopping"),
                            Name = Lang.Get("Chop", Array.Empty<object>())
                        }
                };

                if (capi != null)
                {
                    for (int i = 0; i < array.Length; i++)
                    {
                        array[i].WithIcon(capi, capi.Gui.LoadSvgWithPadding(new AssetLocation("indappledgroves:textures/icons/" + array[i].Code.FirstCodePart().ToString() + ".svg"), 48, 48, 5, new int?(-1)));
                    }
                }
                return array;
            });
        }

        #region TreeFelling
        public override float OnBlockBreaking(IPlayer player, BlockSelection blockSel, ItemSlot itemslot, float remainingResistance, float dt, int counter, ref EnumHandling handled)
        {
            ITreeAttribute tempAttr = itemslot.Itemstack.TempAttributes;
            int posx = tempAttr.GetInt("lastposX", -1);
            int posy = tempAttr.GetInt("lastposY", -1);
            int posz = tempAttr.GetInt("lastposZ", -1);
            BlockPos pos = blockSel.Position;
            
                float treeResistance;
                if (pos.X != posx || pos.Y != posy || pos.Z != posz || counter % 30 == 0)
                {
                    float baseResistance;
                    int woodTier;
                    this.FindTree(player.Entity.World, pos, out baseResistance, out woodTier);
                    if (collObj.ToolTier < woodTier - 3)
                    {
                        handled = EnumHandling.PreventDefault;
                        return remainingResistance;
                    }
                    treeResistance = (float)Math.Max(1.0, Math.Sqrt((double)baseResistance / 1.45)) * IDGTreeConfig.Current.TreeFellingMultiplier;
                    tempAttr.SetFloat("treeResistance", treeResistance);
                }
                else
                {
                    treeResistance = tempAttr.GetFloat("treeResistance", 1f);
                }
                tempAttr.SetInt("lastposX", pos.X);
                tempAttr.SetInt("lastposY", pos.Y);
                tempAttr.SetInt("lastposZ", pos.Z);

                float treeFellingMultiplier = collObj.Attributes["choppingprops"]["fellingmultiplier"].AsFloat();
                float treeDmg = remainingResistance - ((collObj.GetMiningSpeed(itemslot.Itemstack, blockSel, api.World.BlockAccessor.GetBlock(pos), player) * treeFellingMultiplier) * dt);
                remainingResistance = treeDmg;
            handled = EnumHandling.PreventDefault;
            return remainingResistance;            
        }
        
        public Stack<BlockPos> FindTree(IWorldAccessor world, BlockPos startPos, out float resistance, out int woodTier)
        {
            Queue<Vec4i> queue = new();
            HashSet<BlockPos> checkedPositions = new();
            Stack<BlockPos> foundPositions = new();
            Block startBlock = api.World.BlockAccessor.GetBlock(startPos);
            BlockPos secondPos = null;
            resistance = 0;
            woodTier = 0;

            api.World.BlockAccessor.WalkBlocks(startPos.AddCopy(1, 1, 1), startPos.AddCopy(-1, 1, -1), (block, x, y, z) =>
            {
                string[] woods = new[] { "log", "ferntree", "fruittree", "bamboo", "lognarrow","logsection"};
                if (woods.Contains<string>(block.Code.FirstCodePart())) { secondPos = new BlockPos(x, y+1, z, 0); }
            }, true);

            if (startBlock.Code.FirstCodePart().Contains("treestump"))
            {
                startPos = secondPos != null ? secondPos : startPos;
            }

            Block block = world.BlockAccessor.GetBlock(startPos, 0);

            if (block.Code == null) return foundPositions;

            JsonObject attributes = block.Attributes;
            string treeFellingGroupCode = attributes?["treeFellingGroupCode"].AsString();
            JsonObject attributes2 = block.Attributes;
            int spreadIndex = attributes2?["treeFellingGroupSpreadIndex"].AsInt(0) ?? 0;
            JsonObject attributes3 = block.Attributes;

            if (attributes3 != null && !attributes3["treeFellingCanChop"].AsBool(true))
            {
                return foundPositions;
            }

            // Must start with a log
            EnumTreeFellingBehavior bh = EnumTreeFellingBehavior.Chop;
            ICustomTreeFellingBehavior ctfbh = block as ICustomTreeFellingBehavior;
            if (ctfbh != null)
            {
                bh = ctfbh.GetTreeFellingBehavior(startPos, null, spreadIndex);
                if (bh == EnumTreeFellingBehavior.NoChop)
                {
                    return foundPositions;
                }
            }

            if (spreadIndex < 2) return foundPositions;

            if (treeFellingGroupCode == null) return foundPositions;

            string treeFellingGroupLeafCode = null;

            queue.Enqueue(new Vec4i(startPos.X, startPos.Y, startPos.Z, spreadIndex));
            foundPositions.Push(startPos);
            checkedPositions.Add(startPos);

            while (queue.Count > 0 && foundPositions.Count <= 2500)
            {

                Vec4i pos = queue.Dequeue();
                block = world.BlockAccessor.GetBlock(new BlockPos(pos.X, pos.Y, pos.Z, 0));
                ICustomTreeFellingBehavior ctfbhh = block as ICustomTreeFellingBehavior;
                if (ctfbhh != null)
                {
                    bh = ctfbhh.GetTreeFellingBehavior(startPos, null, spreadIndex);
                }
                if (bh != EnumTreeFellingBehavior.NoChop)
                {
                    for (int i = 0; i < Vec3i.DirectAndIndirectNeighbours.Length; i++)
                    {
                        Vec3i facing = Vec3i.DirectAndIndirectNeighbours[i];
                        BlockPos neibPos = new(pos.X + facing.X, pos.Y + facing.Y, pos.Z + facing.Z, 0);

                        float hordist = GameMath.Sqrt(neibPos.HorDistanceSqTo((double)startPos.X, (double)startPos.Z));
                        float vertdist = (float)(neibPos.Y - startPos.Y);
                        float f = (bh == EnumTreeFellingBehavior.ChopSpreadVertical) ? 0.5f : 2f;

                        // "only breaks blocks inside an upside down square base pyramid"
                        if (hordist - 1f < f * vertdist && !checkedPositions.Contains(neibPos))
                        {
                            block = world.BlockAccessor.GetBlock(neibPos);
                            if (!(block.Code == null) && block.Id != 0)
                            {
                                JsonObject attributes4 = block.Attributes;
                                string ngcode = (attributes4 != null) ? attributes4["treeFellingGroupCode"].AsString(null) : null;
                                if (ngcode != treeFellingGroupCode)
                                {
                                    if (ngcode == null)
                                    {
                                        goto IL_32A;
                                    }
                                    if (ngcode != treeFellingGroupLeafCode)
                                    {
                                        if (treeFellingGroupLeafCode != null || block.BlockMaterial != EnumBlockMaterial.Leaves || ngcode.Length != treeFellingGroupCode.Length + 1 || !ngcode.EndsWith(treeFellingGroupCode))
                                        {
                                            goto IL_32A;
                                        }
                                        treeFellingGroupLeafCode = ngcode;
                                    }
                                }
                                JsonObject attributes5 = block.Attributes;
                                int nspreadIndex = (attributes5 != null) ? attributes5["treeFellingGroupSpreadIndex"].AsInt(0) : 0;
                                if (pos.W >= nspreadIndex)
                                {
                                    checkedPositions.Add(neibPos);
                                    if (bh != EnumTreeFellingBehavior.ChopSpreadVertical || facing.Equals(0, 1, 0) || nspreadIndex <= 0)
                                    {
                                        resistance += block.Resistance;
                                        if (woodTier == 0)
                                        {
                                            woodTier = nspreadIndex;
                                        }
                                        foundPositions.Push(neibPos.Copy());
                                        queue.Enqueue(new Vec4i(neibPos.X, neibPos.Y, neibPos.Z, nspreadIndex));
                                    }
                                }
                            }
                        }
                    IL_32A:;
                    }
                }
            }
            return foundPositions;
        }

        public override bool OnBlockBrokenWith(IWorldAccessor world, Entity byEntity, ItemSlot itemslot, BlockSelection blockSel, float dropQuantityMultiplier, ref EnumHandling bhHandling)
        {
            //This method exists solely to aid compatibility with Give Me One Seed Please. It must be here, implemented and non-virtual, for their patch to work.
            return base.OnBlockBrokenWith(world, byEntity, itemslot, blockSel, dropQuantityMultiplier, ref bhHandling);
        }

        

        #endregion TreeFelling

        //Create function by which interactions will find recipes using the target block and the current tool mode.
        public SkillItem[] toolModes;
    }
}
