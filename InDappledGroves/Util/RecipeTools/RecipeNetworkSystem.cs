using ProtoBuf;
using System.Collections.Generic;
using System.IO;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using static InDappledGroves.Util.RecipeTools.IDGRecipeNames;

namespace InDappledGroves.Util.RecipeTools
{
    [ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
    public class RecipeUpload
    {
        public List<string> bwsvalues;
        public List<string> cwsvalues;
        public List<string> gvalues;
    }

    [ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
    public class RecipeResponse
    {
        public string response;
    }

    public class RecipeUploadSystem : ModSystem
    {
        #region Client
        IClientNetworkChannel clientChannel;
        ICoreClientAPI clientApi;

        // Holds a recipe packet that arrived before world items were loaded
        private RecipeUpload pendingMessage;

        public override void StartClientSide(ICoreClientAPI api)
        {
            clientApi = api;

            clientChannel =
                api.Network.RegisterChannel("idgrecipechannel")
                .RegisterMessageType(typeof(RecipeUpload))
                .RegisterMessageType(typeof(RecipeResponse))
                .SetMessageHandler<RecipeUpload>(OnServerMessage)
            ;

            // Process any buffered packet once the world is ready
            api.Event.LevelFinalize += OnBlockTexturesLoaded;
        }

        private void OnServerMessage(RecipeUpload networkMessage)
        {
            // If items haven't loaded yet, buffer the packet until LevelFinalize
            if (clientApi.World.Items.Count > 0)
            {
                ProcessMessage(networkMessage);
            }
            else
            {
                pendingMessage = networkMessage;
            }
        }

        private void OnBlockTexturesLoaded()
        {
            if (pendingMessage != null)
            {
                ProcessMessage(pendingMessage);
                pendingMessage = null;
            }
        }

        private void ProcessMessage(RecipeUpload networkMessage)
        {
            List<BasicWorkstationRecipe> bwsrecipes = new();
            List<ComplexWorkstationRecipe> cwsrecipes = new();
            List<GroundRecipe> grecipes = new();

            if (networkMessage.gvalues != null)
            {
                foreach (string grec in networkMessage.gvalues)
                {
                    using (MemoryStream ms = new(Ascii85.Decode(grec)))
                    {
                        BinaryReader reader = new BinaryReader(ms);
                        GroundRecipe retr = new GroundRecipe();
                        retr.FromBytes(reader, clientApi.World);
                        grecipes.Add(retr);
                    }
                }
            }
            IDGRecipeRegistry.Loaded.GroundRecipes = grecipes;

            if (networkMessage.bwsvalues != null)
            {
                foreach (string bwsrec in networkMessage.bwsvalues)
                {
                    using (MemoryStream ms = new(Ascii85.Decode(bwsrec)))
                    {
                        BinaryReader reader = new BinaryReader(ms);
                        BasicWorkstationRecipe retr = new BasicWorkstationRecipe();
                        retr.FromBytes(reader, clientApi.World);
                        bwsrecipes.Add(retr);
                    }
                }
            }
            IDGRecipeRegistry.Loaded.BasicWorkstationRecipes = bwsrecipes;

            if (networkMessage.cwsvalues != null)
            {
                foreach (string cwsrec in networkMessage.cwsvalues)
                {
                    using (MemoryStream ms = new(Ascii85.Decode(cwsrec)))
                    {
                        BinaryReader reader = new BinaryReader(ms);
                        ComplexWorkstationRecipe retr = new ComplexWorkstationRecipe();
                        retr.FromBytes(reader, clientApi.World);
                        cwsrecipes.Add(retr);
                    }
                }
            }
            IDGRecipeRegistry.Loaded.ComplexWorkstationRecipes = cwsrecipes;
        }
        #endregion

        #region Server
        IServerNetworkChannel serverChannel;
        ICoreServerAPI serverApi;

        public override void StartServerSide(ICoreServerAPI api)
        {
            serverApi = api;

            serverChannel =
                api.Network.RegisterChannel("idgrecipechannel")
                .RegisterMessageType(typeof(RecipeUpload))
                .RegisterMessageType(typeof(RecipeResponse))
                .SetMessageHandler<RecipeResponse>(OnClientMessage)
            ;

            api.RegisterCommand("recipeupload", "Resync recipes", "", OnRecipeUploadCmd, Privilege.chat);
            api.Event.PlayerNowPlaying += (hmm) => { OnRecipeUploadCmd(); };
        }

        private void OnRecipeUploadCmd(IServerPlayer player = null, int groupId = 0, CmdArgs args = null)
        {
            List<string> bwsrecipes = new List<string>();
            List<string> cwsrecipes = new List<string>();
            List<string> grecipes = new List<string>();

            foreach (BasicWorkstationRecipe bwsrec in IDGRecipeRegistry.Loaded.BasicWorkstationRecipes)
            {
                using (MemoryStream ms = new MemoryStream())
                {
                    BinaryWriter writer = new BinaryWriter(ms);
                    bwsrec.ToBytes(writer);
                    bwsrecipes.Add(Ascii85.Encode(ms.ToArray()));
                }
            }

            foreach (ComplexWorkstationRecipe cwsrec in IDGRecipeRegistry.Loaded.ComplexWorkstationRecipes)
            {
                using (MemoryStream ms = new MemoryStream())
                {
                    BinaryWriter writer = new BinaryWriter(ms);
                    cwsrec.ToBytes(writer);
                    cwsrecipes.Add(Ascii85.Encode(ms.ToArray()));
                }
            }

            foreach (GroundRecipe grec in IDGRecipeRegistry.Loaded.GroundRecipes)
            {
                using (MemoryStream ms = new MemoryStream())
                {
                    BinaryWriter writer = new BinaryWriter(ms);
                    grec.ToBytes(writer);
                    grecipes.Add(Ascii85.Encode(ms.ToArray()));
                }
            }

            serverChannel.BroadcastPacket(new RecipeUpload()
            {
                bwsvalues = bwsrecipes,
                cwsvalues = cwsrecipes,
                gvalues = grecipes,
            });
        }

        private void OnClientMessage(IPlayer fromPlayer, RecipeResponse networkMessage)
        {
            OnRecipeUploadCmd();
        }
        #endregion
    }
}
