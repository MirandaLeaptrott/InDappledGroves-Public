using ProtoBuf;
using Vintagestory.API.Common;
using Vintagestory.API.Server;

namespace InDappledGroves.Util.Config
{
    [ProtoBuf.ProtoContract()]
    public class IDGToolConfig
    {
        [ProtoMember(1)]
        //Multiplier applied to the Mining Speed of a tool used at a Workstation. Should default to 1. Less than 1 slows down, more than 1 speeds up work.
        public float baseWorkstationMiningSpdMult { get; set; }

        [ProtoMember(2)]
        //Multiplier applied to the Resistance of a block being worked on a workstation. Should default to 1. Less than 1 speeds up, more than 1 slows down work.
        public float baseWorkstationResistanceMult { get; set; }
        
        [ProtoMember(3)]
        //Multiplier applied to the Mining Speed of a tool used in a ground recipe. Should default to 1. Less than 1 slows down, more than 1 speeds up work.
        public float baseGroundRecipeMiningSpdMult { get; set; }
        
        [ProtoMember(4)]
        //Multiplier applied to the Resistance of a block being worked on the ground. Should default to 1. Less than 1 speeds up, more than 1 slows down work.
        public float baseGroundRecipeResistanceMult { get; set; }

        [ProtoMember(5)]
        //Current Version of the mod, ensures consistency with most recent config paradigm
        public float ConfigVersion { get; set; }

        public IDGToolConfig()
        { }

        public static IDGToolConfig Current { get; set; }

        public static IDGToolConfig GetDefault()
        {
            IDGToolConfig defaultConfig = new();

            defaultConfig.baseWorkstationMiningSpdMult = 0.33f;
            defaultConfig.baseWorkstationResistanceMult = 1f;
            defaultConfig.baseGroundRecipeMiningSpdMult = 1f;
            defaultConfig.baseGroundRecipeResistanceMult = 1f;
            defaultConfig.ConfigVersion = 1.0f;

            return defaultConfig;
        }
        
        public static void createConfigFile(ICoreAPI api)
        {
            //Tool/Workstation Config
            //Check for Existing Config file, create one if none exists
            try
            {
                var Config = api.LoadModConfig<IDGToolConfig>("indappledgroves/toolconfig.json");
                if (Config != null)
                {
                    if (Config.ConfigVersion == GetDefault().ConfigVersion)
                    {
                        api.Logger.Notification("Mod Config successfully loaded.");
                        IDGToolConfig.Current = Config;
                    } else
                    {
                        api.Logger.Notification("Config Version Out Of Date, Updating To Most Recent Default Config");
                        IDGToolConfig.Current = IDGToolConfig.GetDefault();
                    }
                }
                else
                {
                    api.Logger.Notification("No Mod Config specified. Falling back to default settings");
                    IDGToolConfig.Current = IDGToolConfig.GetDefault();
                }
            }
            catch
            {
                IDGToolConfig.Current = IDGToolConfig.GetDefault();
                api.Logger.Error("Failed to load custom mod configuration. Falling back to default settings!");
            }
            finally
            {
                api.StoreModConfig(IDGToolConfig.Current, "indappledgroves/toolconfig.json");
            }
        }
    }
}
