using Vintagestory.API.Common;
using Vintagestory.GameContent;

namespace InDappledGroves.BlockEntities
{
    // Simple display entity for the sawhorse block, holds items for visual rendering
    internal class IDGBESawHorse : BlockEntityDisplay
    {
        private readonly InventoryGeneric inv;

        public IDGBESawHorse()
        {
            inv = new InventoryGeneric(1, "sawhorse", null);
        }

        public override InventoryBase Inventory => inv;

        public override string InventoryClassName => "sawhorse";

        protected override float[][] genTransformationMatrices()
        {
            return new float[1][];
        }
    }
}
