using InDappledGroves.Util.Config;
using ProtoBuf;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Server;

namespace InDappledGroves.Util.Network
{
    public class NetworkHandler
    {
        internal void RegisterMessages(ICoreAPI api)
        {
            api.Network
                .RegisterChannel("idgnetwork")
                .RegisterMessageType(typeof(NetworkApiTestMessage))
                .RegisterMessageType(typeof(NetworkApiTestResponse))
                .RegisterMessageType(typeof(ToolConfigFromServerMessage))
                .RegisterMessageType(typeof(TreeConfigFromServerMessage))
                .RegisterMessageType(typeof(OnPlayerLoginMessage));
            ; 
        }

        #region Client
        IClientNetworkChannel clientChannel;
        ICoreClientAPI clientApi;
        public void InitializeClientSideNetworkHandler(ICoreClientAPI capi) {
            clientApi = capi;

            clientChannel = capi.Network.GetChannel("idgnetwork")
                .SetMessageHandler<ToolConfigFromServerMessage>(RecieveToolConfigAction)
                .SetMessageHandler<TreeConfigFromServerMessage>(RecieveTreeConfigAction);
            ;

        }


        //SetToolConfigValues received from Server
        private void RecieveToolConfigAction(ToolConfigFromServerMessage toolConfig)
        {
            //Fired when the server sends the ToolConfig information to the player's client after login

            //Set Client Tool Config Settings from Server
            InDappledGroves.baseWorkstationMiningSpdMult = toolConfig.baseWorkstationMiningSpdMult;
            InDappledGroves.baseWorkstationResistanceMult = toolConfig.baseWorkstationResistanceMult;
            InDappledGroves.baseGroundRecipeMiningSpdMult = toolConfig.baseGroundRecipeMiningSpdMult;
            InDappledGroves.baseGroundRecipeResistaceMult = toolConfig.baseGroundRecipeResistanceMul;

            IDGToolConfig.Current.baseWorkstationMiningSpdMult = toolConfig.baseWorkstationMiningSpdMult;
            IDGToolConfig.Current.baseWorkstationResistanceMult = toolConfig.baseWorkstationResistanceMult;
            // Was swapped: miningSpdMult was assigned ResistanceMul and vice versa
            IDGToolConfig.Current.baseGroundRecipeMiningSpdMult = toolConfig.baseGroundRecipeMiningSpdMult;
            IDGToolConfig.Current.baseGroundRecipeResistanceMult = toolConfig.baseGroundRecipeResistanceMul;
            IDGToolConfig.Current.ConfigVersion = toolConfig.ConfigVersion;
        }

        private void RecieveTreeConfigAction(TreeConfigFromServerMessage treeConfig)
        {
            //Fired when the server sends the TreeConfig information to the player's client after login

            //Set Client Tree Config Settings from Server
            IDGTreeConfig.Current.ConfigVersion = treeConfig.TreeFellingMultiplier;
            IDGTreeConfig.Current.MinHorizontalSaplingDistance = treeConfig.MinHorizontalSaplingDistance;
            IDGTreeConfig.Current.MinVerticalSaplingDistance = treeConfig.MinVerticalSaplingDistance;
            IDGTreeConfig.Current.ConfigVersion = treeConfig.ConfigVersion;
        }

        #endregion

        #region server
        IServerNetworkChannel serverChannel;
        ICoreServerAPI serverApi;
        public void InitializeServerSideNetworkHandler(ICoreServerAPI api)
        {
            serverApi = api;

            //Listen for player join events
            api.Event.PlayerJoin += OnPlayerJoin;

            serverChannel = api.Network.GetChannel("idgnetwork")
                .SetMessageHandler<OnPlayerLoginMessage>(OnPlayerJoin);
        }

        private void OnPlayerJoin(IServerPlayer player)
        {
            OnPlayerJoin(player, new OnPlayerLoginMessage());
        }

        //Send a packet on the client channel containing a new instance of ToolConfigFromServerMessage
        //Which is pre-loaded on creation with all the values for Tool Config.
        private void OnPlayerJoin(IServerPlayer fromPlayer, OnPlayerLoginMessage packet)
        {
            serverChannel.SendPacket(new ToolConfigFromServerMessage(), fromPlayer);
            serverChannel.SendPacket(new TreeConfigFromServerMessage(), fromPlayer);
        }

        #endregion

        [ProtoContract]
        class NetworkApiTestMessage
        {
            [ProtoMember(1)]
            public string message { get; set; }
        }

        [ProtoContract]
        class NetworkApiTestResponse
        {
            [ProtoMember(1)]
            public string response { get; set; }
        }

        [ProtoContract]
        class ToolConfigFromServerMessage
        {

            [ProtoMember(1)]
            public float baseWorkstationMiningSpdMult = IDGToolConfig.Current.baseWorkstationMiningSpdMult;
            [ProtoMember(2)]
            public float baseWorkstationResistanceMult = IDGToolConfig.Current.baseWorkstationResistanceMult;
            [ProtoMember(3)]
            public float baseGroundRecipeMiningSpdMult = IDGToolConfig.Current.baseGroundRecipeMiningSpdMult;
            [ProtoMember(4)]
            public float baseGroundRecipeResistanceMul = IDGToolConfig.Current.baseGroundRecipeResistanceMult;
            [ProtoMember(5)]
            public float ConfigVersion = IDGToolConfig.Current.ConfigVersion;

        }

        [ProtoContract]
        class TreeConfigFromServerMessage
        {
            [ProtoMember(1)]
            public float TreeFellingMultiplier = IDGTreeConfig.Current.TreeFellingMultiplier;
            //Rate at which Tree Hollows Update

            [ProtoMember(2)]
            public bool SaplingSpacingEnabled = IDGTreeConfig.Current.SaplingSpacingEnabled;

            [ProtoMember(3)]
            public int MinHorizontalSaplingDistance = IDGTreeConfig.Current.MinHorizontalSaplingDistance;

            [ProtoMember(4)]
            public int MinVerticalSaplingDistance = IDGTreeConfig.Current.MinVerticalSaplingDistance;

            [ProtoMember(5)]
            public float ConfigVersion = IDGTreeConfig.Current.ConfigVersion;

        }


        [ProtoContract]
        class OnPlayerLoginMessage
        {
        }
    }
}

