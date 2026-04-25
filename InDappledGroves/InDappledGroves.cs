using InDappledGroves.BlockBehaviors;
using InDappledGroves.BlockEntities;
using InDappledGroves.Blocks;
using InDappledGroves.CollectibleBehaviors;
using InDappledGroves.Items;
using InDappledGroves.Util.Config;
using InDappledGroves.Util.Network;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Server;

namespace InDappledGroves
{
    public class InDappledGroves : ModSystem
    {
        internal static float baseWorkstationMiningSpdMult;
        internal static float baseWorkstationResistanceMult;
        internal static float baseGroundRecipeMiningSpdMult;
        internal static float baseGroundRecipeResistaceMult;

        private NetworkHandler networkHandler;

        public override bool ShouldLoad(EnumAppSide forSide)
        {
            return true;
        }

        public override void StartClientSide(ICoreClientAPI api)
        {
            networkHandler.InitializeClientSideNetworkHandler(api);
        }

        public override void StartServerSide(ICoreServerAPI api)
        {
            networkHandler.InitializeServerSideNetworkHandler(api);
        }

        public override void Start(ICoreAPI api)
        {
            networkHandler = new NetworkHandler();
            base.Start(api);

            api.RegisterItemClass("idgfirewood", typeof(IDGFirewood));
            api.RegisterItemClass("idgplank", typeof(IDGPlank));
            api.RegisterItemClass("idgbark", typeof(IDGBark));
            api.RegisterItemClass("idgtreeseed", typeof(IDGTreeSeed));

            api.RegisterBlockClass("idgbarkbundle", typeof(IDGBarkBundle));
            api.RegisterBlockClass("idglogslab", typeof(IDGLogSlab));
            api.RegisterBlockClass("idgworkstation", typeof(IDGWorkstation));
            api.RegisterBlockClass("idgbarkbasket", typeof(IDGBarkBasket));
            api.RegisterBlockClass("idgboardblock", typeof(IDGBoardBlock));
            api.RegisterBlockClass("idgblockfirewood", typeof(IDGBlockFirewood));

            api.RegisterBlockEntityClass("idgbeworkstation", typeof(IDGBEWorkstation));
            api.RegisterBlockEntityClass("idglogsplitter", typeof(BlockEntityLogSplitter));

            api.RegisterCollectibleBehaviorClass("woodsplitter", typeof(BehaviorWoodChopping));
            api.RegisterCollectibleBehaviorClass("woodsawer", typeof(BehaviorWoodSawing));
            api.RegisterCollectibleBehaviorClass("woodplaner", typeof(BehaviorWoodPlaning));
            api.RegisterCollectibleBehaviorClass("woodhewer", typeof(BehaviorWoodHewing));
            api.RegisterCollectibleBehaviorClass("idgtool", typeof(BehaviorIDGTool));
            api.RegisterCollectibleBehaviorClass("pounder", typeof(BehaviorPounding));

            api.RegisterBlockBehaviorClass("Submergible", typeof(BehaviorSubmergible));
            api.RegisterBlockBehaviorClass("IDGPickup", typeof(BehaviorIDGPickup));

            networkHandler.RegisterMessages(api);

            IDGToolConfig.createConfigFile(api);
            IDGTreeConfig.CreateConfigFile(api);
        }
    }
}
