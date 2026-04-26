using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;

namespace InDappledGroves.BlockEntities
{
    public class BlockEntityLogSplitter : IDGBEWorkstation
    {
        protected override float[][] genTransformationMatrices()
        {
            float[][] tfMatrices = new float[Inventory.Count][];
            for (int index = 0; index < Inventory.Count; index++)
            {
                ItemSlot itemSlot = this.Inventory[index];
                if (itemSlot == null || itemSlot.Itemstack == null) continue;

                if (index == Block.Attributes["workstationproperties"]["slottypes"]["inputslot"].AsInt())
                {
                    tfMatrices[index] = new Matrixf().Translate(0.5, 0.5, 0.5).RotateYDeg(this.Block.Shape.rotateY).Translate(0, 0 - Math.Min(Math.Ceiling((this.recipeHandler.recipeProgress) * 4 - 0.225) / 4, 0.725), 0).Translate(-0.5, -0.5, -0.5).Values;
                }
                else
                {
                    tfMatrices[index] = new Matrixf().Translate(0.5, 0.5, 0.5).RotateYDeg(this.Block.Shape.rotateY).Translate(-0.5, -0.5, -0.5).Values;
                }
            }
            return tfMatrices;
        }
    }
}
