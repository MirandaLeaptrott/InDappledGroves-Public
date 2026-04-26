using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Server;
using Vintagestory.API.Util;
using Vintagestory.GameContent;


namespace InDappledGroves.Util.RecipeTools
{
    public class IDGRecipeNames : ICookingRecipeNamingHelper
    {
        public string GetNameForIngredients(IWorldAccessor worldForResolve, string recipeCode, ItemStack[] stacks)
        {
            return "Cheat Code";
        }

        public class IDGRecipeRegistry
        {
            private static IDGRecipeRegistry loaded;
            private List<BasicWorkstationRecipe> workstationrecipes = new List<BasicWorkstationRecipe>();
            private List<GroundRecipe> groundRecipes = new List<GroundRecipe>();
            private List<ComplexWorkstationRecipe> complexWorkstationRecipes = new List<ComplexWorkstationRecipe>();

            public List<BasicWorkstationRecipe> BasicWorkstationRecipes
            {
                get { return workstationrecipes; }
                set { workstationrecipes = value; }
            }

            public List<ComplexWorkstationRecipe> ComplexWorkstationRecipes
            {
                get { return complexWorkstationRecipes; }
                set { complexWorkstationRecipes = value; }
            }

            public List<GroundRecipe> GroundRecipes
            {
                get { return groundRecipes; }
                set { groundRecipes = value; }
            }

            public static IDGRecipeRegistry Create()
            {
                if (loaded == null) loaded = new IDGRecipeRegistry();
                return Loaded;
            }

            public static IDGRecipeRegistry Loaded
            {
                get
                {
                    if (loaded == null) loaded = new IDGRecipeRegistry();
                    return loaded;
                }
            }

            public static void Dispose()
            {
                if (loaded == null) return;
                loaded = null;
            }
        }

        public class IDGRecipeLoader : ModSystem
        {
            private ICoreServerAPI api;

            public override double ExecuteOrder() => 100;

            public override void AssetsFinalize(ICoreAPI capi)
            {
                IDGRecipeRegistry.Create();
                LoadIDGRecipes();
                base.AssetsFinalize(capi);
            }

            public override void AssetsLoaded(ICoreAPI api)
            {
                if (!(api is ICoreServerAPI sapi)) return;
                this.api = sapi;
            }

            public override void Dispose()
            {
                base.Dispose();
                IDGRecipeRegistry.Dispose();
            }

            public void LoadIDGRecipes()
            {
                api.World.Logger.StoryEvent(Lang.Get("indappledgroves:The Tyee and the bullcook..."));
                LoadGroundRecipes();
                LoadWorkStationRecipes();
                LoadComplexWorkstationRecipes();
            }

            #region WorkStation Base Recipes
            public void LoadWorkStationRecipes()
            {
                Dictionary<AssetLocation, JToken> files = api.Assets.GetMany<JToken>(api.Server.Logger, "recipes/workstation/basic");
                int recipeQuantity = 0;
                int ignored = 0;
                int orphaned = 0;
                Dictionary<string, int> recipeList = new Dictionary<string, int>();
                foreach (KeyValuePair<AssetLocation, JToken> val in files)
                {
                    if (val.Value is JObject)
                    {
                        try
                        {
                            BasicWorkstationRecipe rec = val.Value.ToObject<BasicWorkstationRecipe>();
                            if (!rec.Enabled) continue;
                            if (rec.RequiredWorkstation == null) { orphaned++; continue; }
                            LoadWorkStationRecipe(val.Key, rec, ref recipeQuantity, ref ignored);
                            if (!recipeList.TryAdd(rec.RequiredWorkstation, 1)) recipeList[rec.RequiredWorkstation]++;
                        }
                        catch (Exception ex)
                        {
                            api.World.Logger.Error("Skipping workstation recipe {0}: {1}", val.Key, ex.Message);
                            ignored++;
                        }
                    }
                    else if (val.Value is JArray)
                    {
                        int idx = 0;
                        foreach (JToken token in (JArray)val.Value)
                        {
                            try
                            {
                                BasicWorkstationRecipe rec = token.ToObject<BasicWorkstationRecipe>();
                                if (!rec.Enabled) { idx++; continue; }
                                if (rec.RequiredWorkstation == "none") { orphaned++; idx++; continue; }
                                LoadWorkStationRecipe(val.Key, rec, ref recipeQuantity, ref ignored);
                                if (!recipeList.TryAdd(rec.RequiredWorkstation, 1)) recipeList[rec.RequiredWorkstation]++;
                            }
                            catch (Exception ex)
                            {
                                api.World.Logger.Error("Skipping workstation recipe {0}[{1}]: {2}", val.Key, idx, ex.Message);
                                ignored++;
                            }
                            idx++;
                        }
                    }
                }
                foreach (KeyValuePair<string, int> kvp in recipeList)
                {
                    api.World.Logger.Event("{1} {0} recipes loaded", kvp.Key, kvp.Value);
                }
                api.World.Logger.Event("{0} workstation recipes successfully loaded", recipeQuantity);
                api.World.Logger.Event("{0} workstation recipes orphaned due to RequiredWorkstation not being set.", orphaned);
            }

            public void LoadWorkStationRecipe(AssetLocation path, BasicWorkstationRecipe recipe, ref int quantityRegistered, ref int quantityIgnored)
            {
                if (!recipe.Enabled) return;
                if (recipe.Name == null) recipe.Name = path;

                Dictionary<string, string[]> nameToCodeMapping = recipe.GetNameToCodeMapping(api.World);

                if (nameToCodeMapping.Count > 0)
                {
                    List<BasicWorkstationRecipe> subRecipes = new List<BasicWorkstationRecipe>();

                    int qCombs = 0;
                    bool first = true;
                    foreach (var val2 in nameToCodeMapping)
                    {
                        if (first) qCombs = val2.Value.Length;
                        else qCombs *= val2.Value.Length;
                        first = false;
                    }

                    first = true;
                    foreach (var val2 in nameToCodeMapping)
                    {
                        string variantCode = val2.Key;
                        string[] variants = val2.Value;

                        for (int i = 0; i < qCombs; i++)
                        {
                            BasicWorkstationRecipe rec;
                            if (first) subRecipes.Add(rec = recipe.Clone());
                            else rec = subRecipes[i];

                            if (rec.Ingredients != null)
                            {
                                foreach (var ingreds in rec.Ingredients)
                                {
                                    if (ingreds.Inputs.Length <= 0) continue;
                                    CraftingRecipeIngredient ingred = ingreds.Inputs[0];
                                    if (ingred.Name == variantCode)
                                        ingred.Code = ingred.Code.CopyWithPath(ingred.Code.Path.Replace("*", variants[i % variants.Length]));
                                }
                            }

                            rec.ReturnStack.FillPlaceHolder(val2.Key, variants[i % variants.Length]);
                            rec.Output.FillPlaceHolder(val2.Key, variants[i % variants.Length]);
                        }

                        first = false;
                    }

                    if (subRecipes.Count == 0)
                        api.World.Logger.Warning("File {0} make uses of wildcards, but no blocks or item matching those wildcards were found.", path);

                    foreach (BasicWorkstationRecipe subRecipe in subRecipes)
                    {
                        if (!subRecipe.Resolve(api.World, path)) { quantityIgnored++; continue; }
                        IDGRecipeRegistry.Loaded.BasicWorkstationRecipes.Add(subRecipe);
                        quantityRegistered++;
                    }
                }
                else
                {
                    if (!recipe.Resolve(api.World, path)) { quantityIgnored++; return; }
                    IDGRecipeRegistry.Loaded.BasicWorkstationRecipes.Add(recipe);
                    quantityRegistered++;
                }
            }

            public class WorkStationIngredient : IByteSerializable
            {
                public CraftingRecipeIngredient[] Inputs;

                public CraftingRecipeIngredient GetMatch(ItemStack stack)
                {
                    if (stack == null) return null;
                    for (int i = 0; i < Inputs.Length; i++)
                        if (Inputs[i].SatisfiesAsIngredient(stack)) return Inputs[i];
                    return null;
                }

                public bool Resolve(IWorldAccessor world, string debug)
                {
                    bool ok = true;
                    for (int i = 0; i < Inputs.Length; i++) ok &= Inputs[i].Resolve(world, debug);
                    return ok;
                }

                public void FromBytes(BinaryReader reader, IWorldAccessor resolver)
                {
                    Inputs = new CraftingRecipeIngredient[reader.ReadInt32()];
                    for (int i = 0; i < Inputs.Length; i++)
                    {
                        Inputs[i] = new CraftingRecipeIngredient();
                        Inputs[i].FromBytes(reader, resolver);
                        Inputs[i].Resolve(resolver, "Workstation Ingredient (FromBytes)");
                    }
                }

                public void ToBytes(BinaryWriter writer)
                {
                    writer.Write(Inputs.Length);
                    for (int i = 0; i < Inputs.Length; i++) Inputs[i].ToBytes(writer);
                }

                public WorkStationIngredient Clone()
                {
                    CraftingRecipeIngredient[] newings = new CraftingRecipeIngredient[Inputs.Length];
                    for (int i = 0; i < Inputs.Length; i++) newings[i] = Inputs[i].Clone();
                    return new WorkStationIngredient() { Inputs = newings };
                }
            }
            #endregion

            #region Splitter Recipes
            public void LoadComplexWorkstationRecipes()
            {
                Dictionary<AssetLocation, JToken> files = api.Assets.GetMany<JToken>(api.Server.Logger, "recipes/workstation/complex");
                int recipeQuantity = 0;
                int ignored = 0;
                Dictionary<string, int> recipeList = new Dictionary<string, int>();
                foreach (KeyValuePair<AssetLocation, JToken> val in files)
                {
                    if (val.Value is JObject)
                    {
                        try
                        {
                            ComplexWorkstationRecipe rec = val.Value.ToObject<ComplexWorkstationRecipe>();
                            if (!rec.Enabled) continue;
                            LoadComplexWorkstationRecipe(val.Key, rec, ref recipeQuantity, ref ignored);
                            if (!recipeList.TryAdd(rec.RequiredWorkstation, 1)) recipeList[rec.RequiredWorkstation]++;
                        }
                        catch (Exception ex)
                        {
                            api.World.Logger.Error("Skipping complex workstation recipe {0}: {1}", val.Key, ex.Message);
                            ignored++;
                        }
                    }
                    else if (val.Value is JArray)
                    {
                        int idx = 0;
                        foreach (JToken token in (JArray)val.Value)
                        {
                            try
                            {
                                ComplexWorkstationRecipe rec = token.ToObject<ComplexWorkstationRecipe>();
                                if (!rec.Enabled) { idx++; continue; }
                                LoadComplexWorkstationRecipe(val.Key, rec, ref recipeQuantity, ref ignored);
                                if (!recipeList.TryAdd(rec.RequiredWorkstation, 1)) recipeList[rec.RequiredWorkstation]++;
                            }
                            catch (Exception ex)
                            {
                                api.World.Logger.Error("Skipping complex workstation recipe {0}[{1}]: {2}", val.Key, idx, ex.Message);
                                ignored++;
                            }
                            idx++;
                        }
                    }
                }
                foreach (KeyValuePair<string, int> kvp in recipeList)
                    api.World.Logger.Event("{1} {0} recipes loaded", kvp.Key, kvp.Value);
                api.World.Logger.Event("{0} workstation recipes successfully loaded", recipeQuantity);
                api.World.Logger.Event("{0} complex workstation recipes loaded", recipeQuantity);
            }

            public void LoadComplexWorkstationRecipe(AssetLocation path, ComplexWorkstationRecipe recipe, ref int quantityRegistered, ref int quantityIgnored)
            {
                if (!recipe.Enabled) return;
                if (recipe.Name == null) recipe.Name = path;

                Dictionary<string, string[]> nameToCodeMapping = recipe.GetNameToCodeMapping(api.World);

                if (nameToCodeMapping.Count > 0)
                {
                    List<ComplexWorkstationRecipe> subRecipes = new List<ComplexWorkstationRecipe>();

                    int qCombs = 0;
                    bool first = true;
                    foreach (var val2 in nameToCodeMapping)
                    {
                        if (first) qCombs = val2.Value.Length;
                        else qCombs *= val2.Value.Length;
                        first = false;
                    }

                    first = true;
                    foreach (var val2 in nameToCodeMapping)
                    {
                        string variantCode = val2.Key;
                        string[] variants = val2.Value;

                        for (int i = 0; i < qCombs; i++)
                        {
                            ComplexWorkstationRecipe rec;
                            if (first) subRecipes.Add(rec = recipe.Clone());
                            else rec = subRecipes[i];

                            if (rec.Ingredients != null)
                            {
                                foreach (var ingreds in rec.Ingredients)
                                {
                                    if (ingreds.Inputs.Length <= 0) continue;
                                    CraftingRecipeIngredient ingred = ingreds.Inputs[0];
                                    if (ingred.Name == variantCode)
                                        ingred.Code = ingred.Code.CopyWithPath(ingred.Code.Path.Replace("*", variants[i % variants.Length]));
                                }
                            }

                            rec.ReturnStack.FillPlaceHolder(val2.Key, variants[i % variants.Length]);
                            rec.Output.FillPlaceHolder(val2.Key, variants[i % variants.Length]);
                        }

                        first = false;
                    }

                    if (subRecipes.Count == 0)
                        api.World.Logger.Warning("{1} file {0} make uses of wildcards, but no blocks or item matching those wildcards were found.", path);

                    foreach (ComplexWorkstationRecipe subRecipe in subRecipes)
                    {
                        if (!subRecipe.Resolve(api.World, path)) { quantityIgnored++; continue; }
                        IDGRecipeRegistry.Loaded.ComplexWorkstationRecipes.Add(subRecipe);
                        quantityRegistered++;
                    }
                }
                else
                {
                    if (!recipe.Resolve(api.World, path)) { quantityIgnored++; return; }
                    IDGRecipeRegistry.Loaded.ComplexWorkstationRecipes.Add(recipe);
                    quantityRegistered++;
                }
            }
            #endregion

            #region Ground Recipes
            public void LoadGroundRecipes()
            {
                Dictionary<AssetLocation, JToken> files = api.Assets.GetMany<JToken>(api.Server.Logger, "recipes/ground");
                int recipeQuantity = 0;
                int ignored = 0;

                foreach (var val in files)
                {
                    if (val.Value is JObject)
                    {
                        try
                        {
                            GroundRecipe rec = val.Value.ToObject<GroundRecipe>();
                            if (!rec.Enabled) continue;
                            LoadGroundRecipe(val.Key, rec, ref recipeQuantity, ref ignored);
                        }
                        catch (Exception ex)
                        {
                            api.World.Logger.Error("Skipping ground recipe {0}: {1}", val.Key, ex.Message);
                            ignored++;
                        }
                    }
                    else if (val.Value is JArray)
                    {
                        int idx = 0;
                        foreach (JToken token in (JArray)val.Value)
                        {
                            try
                            {
                                GroundRecipe rec = token.ToObject<GroundRecipe>();
                                if (!rec.Enabled) { idx++; continue; }
                                LoadGroundRecipe(val.Key, rec, ref recipeQuantity, ref ignored);
                            }
                            catch (Exception ex)
                            {
                                api.World.Logger.Error("Skipping ground recipe {0}[{1}]: {2}", val.Key, idx, ex.Message);
                                ignored++;
                            }
                            idx++;
                        }
                    }
                }

                api.World.Logger.Event("{0} ground recipes loaded", recipeQuantity);
            }

            public void LoadGroundRecipe(AssetLocation path, GroundRecipe recipe, ref int quantityRegistered, ref int quantityIgnored)
            {
                if (!recipe.Enabled) return;
                if (recipe.Name == null) recipe.Name = path;
                string className = "ground recipe";

                Dictionary<string, string[]> nameToCodeMapping = recipe.GetNameToCodeMapping(api.World);

                if (nameToCodeMapping.Count > 0)
                {
                    List<GroundRecipe> subRecipes = new List<GroundRecipe>();

                    int qCombs = 0;
                    bool first = true;
                    foreach (var val2 in nameToCodeMapping)
                    {
                        if (first) qCombs = val2.Value.Length;
                        else qCombs *= val2.Value.Length;
                        first = false;
                    }

                    first = true;
                    foreach (var val2 in nameToCodeMapping)
                    {
                        string variantCode = val2.Key;
                        string[] variants = val2.Value;

                        for (int i = 0; i < qCombs; i++)
                        {
                            GroundRecipe rec;
                            if (first) subRecipes.Add(rec = recipe.Clone());
                            else rec = subRecipes[i];

                            if (rec.Ingredients != null)
                            {
                                foreach (var ingreds in rec.Ingredients)
                                {
                                    if (ingreds.Inputs.Length <= 0) continue;
                                    CraftingRecipeIngredient ingred = ingreds.Inputs[0];
                                    if (ingred.Name == variantCode)
                                        ingred.Code = ingred.Code.CopyWithPath(ingred.Code.Path.Replace("*", variants[i % variants.Length]));
                                }
                            }

                            rec.ReturnStack.FillPlaceHolder(val2.Key, variants[i % variants.Length]);
                            rec.Output.FillPlaceHolder(val2.Key, variants[i % variants.Length]);
                        }

                        first = false;
                    }

                    if (subRecipes.Count == 0)
                        api.World.Logger.Warning("{1} file {0} make uses of wildcards, but no blocks or item matching those wildcards were found.", path, className);

                    foreach (GroundRecipe subRecipe in subRecipes)
                    {
                        if (!subRecipe.Resolve(api.World, className + " " + path)) { quantityIgnored++; continue; }
                        IDGRecipeRegistry.Loaded.GroundRecipes.Add(subRecipe);
                        quantityRegistered++;
                    }
                }
                else
                {
                    if (!recipe.Resolve(api.World, className + " " + path)) { quantityIgnored++; return; }
                    IDGRecipeRegistry.Loaded.GroundRecipes.Add(recipe);
                    quantityRegistered++;
                }
            }

            public class GroundIngredient : IByteSerializable
            {
                public CraftingRecipeIngredient[] Inputs;

                public CraftingRecipeIngredient GetMatch(ItemStack stack)
                {
                    if (stack == null) return null;
                    for (int i = 0; i < Inputs.Length; i++)
                        if (Inputs[i].SatisfiesAsIngredient(stack)) return Inputs[i];
                    return null;
                }

                public bool Resolve(IWorldAccessor world, string debug)
                {
                    bool ok = true;
                    for (int i = 0; i < Inputs.Length; i++) ok &= Inputs[i].Resolve(world, debug);
                    return ok;
                }

                public void FromBytes(BinaryReader reader, IWorldAccessor resolver)
                {
                    Inputs = new CraftingRecipeIngredient[reader.ReadInt32()];
                    for (int i = 0; i < Inputs.Length; i++)
                    {
                        Inputs[i] = new CraftingRecipeIngredient();
                        Inputs[i].FromBytes(reader, resolver);
                        Inputs[i].Resolve(resolver, "Ground Ingredient (FromBytes)");
                    }
                }

                public void ToBytes(BinaryWriter writer)
                {
                    writer.Write(Inputs.Length);
                    for (int i = 0; i < Inputs.Length; i++) Inputs[i].ToBytes(writer);
                }

                public GroundIngredient Clone()
                {
                    CraftingRecipeIngredient[] newings = new CraftingRecipeIngredient[Inputs.Length];
                    for (int i = 0; i < Inputs.Length; i++) newings[i] = Inputs[i].Clone();
                    return new GroundIngredient() { Inputs = newings };
                }
            }
            #endregion
        }

        public class WorkstationRecipe : IByteSerializable
        {
            public string Code = "Work Station Recipe";

            // Fix applied here for nested workstation ingredient type
            public IDGRecipeLoader.WorkStationIngredient[] Ingredients;

            public JsonItemStack Output = new JsonItemStack { Code = new AssetLocation("air"), Type = EnumItemClass.Block, Quantity = 0 };

            public JsonItemStack ReturnStack = new JsonItemStack() { Code = new AssetLocation("air"), Type = EnumItemClass.Block, Quantity = 0 };

            public virtual AssetLocation Name { get; set; }
            public virtual bool Enabled { get; set; } = true;
            public virtual int BaseToolDmg { get; set; } = 1;
            public virtual string ToolMode { get; set; } = "none";
            public virtual string Animation { get; set; } = "axesplit-fp";
            public virtual string Sound { get; set; } = "sounds/block/chop2";
            public virtual string RequiredWorkstation { get; set; } = "none";
            public virtual int IngredientMaterial { get; set; } = 4;
            public virtual double IngredientResistance { get; set; } = 4.0;

            public ItemStack TryCraftNow(ICoreAPI api, ItemSlot inputslots)
            {
                var matched = pairInput(inputslots);
                ItemStack tempstack = Output.ResolvedItemStack.Clone();
                tempstack.StackSize = getOutputSize(matched);
                if (tempstack.StackSize <= 0) return null;
                foreach (var val in matched)
                {
                    val.Key.TakeOut(val.Value.Quantity * (tempstack.StackSize / Output.StackSize));
                    val.Key.MarkDirty();
                }
                return tempstack;
            }

            public bool Matches(IWorldAccessor worldForResolve, ItemSlot inputSlots)
            {
                var matched = pairInput(inputSlots);
                if (matched == null) return false;
                return getOutputSize(matched) >= 0;
            }

            protected List<KeyValuePair<ItemSlot, CraftingRecipeIngredient>> pairInput(ItemSlot inputStacks)
            {
                List<int> alreadyFound = new List<int>();
                Queue<ItemSlot> inputSlotsList = new Queue<ItemSlot>();
                if (!inputStacks.Empty) inputSlotsList.Enqueue(inputStacks);
                if (inputSlotsList.Count != Ingredients.Length) return null;

                List<KeyValuePair<ItemSlot, CraftingRecipeIngredient>> matched = new List<KeyValuePair<ItemSlot, CraftingRecipeIngredient>>();

                while (inputSlotsList.Count > 0)
                {
                    ItemSlot inputSlot = inputSlotsList.Dequeue();
                    bool found = false;
                    for (int i = 0; i < Ingredients.Length; i++)
                    {
                        CraftingRecipeIngredient ingred = Ingredients[i].GetMatch(inputSlot.Itemstack);
                        if (ingred != null && !alreadyFound.Contains(i))
                        {
                            matched.Add(new KeyValuePair<ItemSlot, CraftingRecipeIngredient>(inputSlot, ingred));
                            alreadyFound.Add(i);
                            found = true;
                            break;
                        }
                    }
                    if (!found) return null;
                }

                if (matched.Count != Ingredients.Length) return null;
                return matched;
            }

            internal int getOutputSize(List<KeyValuePair<ItemSlot, CraftingRecipeIngredient>> matched)
            {
                int outQuantityMul = -1;
                foreach (var val in matched)
                {
                    int posChange = val.Key.StackSize / val.Value.Quantity;
                    if (posChange < outQuantityMul || outQuantityMul == -1) outQuantityMul = posChange;
                }
                if (outQuantityMul == -1) return -1;
                foreach (var val in matched)
                    if (val.Key.StackSize < val.Value.Quantity * outQuantityMul) return -1;
                outQuantityMul = 1;
                return outQuantityMul;
            }

            public string GetOutputName()
            {
                return Lang.Get("indappledgroves:Will make {0}", Output.ResolvedItemStack.GetName());
            }

            public bool Resolve(IWorldAccessor world, string sourceForErrorLogging)
            {
                bool ok = true;
                for (int i = 0; i < Ingredients.Length; i++)
                    ok &= Ingredients[i].Resolve(world, sourceForErrorLogging);
                ok &= Output.Resolve(world, sourceForErrorLogging, printWarningOnError: true);
                if (ReturnStack.Quantity == 0 && ReturnStack.Code.ToString() != "air") ReturnStack.Quantity = 1;
                return ok & ReturnStack.Resolve(world, sourceForErrorLogging, printWarningOnError: true);
            }

            public void ToBytes(BinaryWriter writer)
            {
                writer.Write(Code);
                writer.Write(BaseToolDmg);
                writer.Write(ToolMode);
                writer.Write(RequiredWorkstation);
                writer.Write(Animation);
                writer.Write(Sound);
                writer.Write(IngredientMaterial);
                writer.Write(IngredientResistance);
                writer.Write(Ingredients.Length);
                for (int i = 0; i < Ingredients.Length; i++) Ingredients[i].ToBytes(writer);
                Output.ToBytes(writer);
                ReturnStack.ToBytes(writer);
            }

            public void FromBytes(BinaryReader reader, IWorldAccessor resolver)
            {
                Code = reader.ReadString();
                BaseToolDmg = reader.ReadInt32();
                ToolMode = reader.ReadString();
                RequiredWorkstation = reader.ReadString();
                Animation = reader.ReadString();
                Sound = reader.ReadString();
                IngredientMaterial = reader.ReadInt32();
                IngredientResistance = reader.ReadDouble();
                Ingredients = new IDGRecipeLoader.WorkStationIngredient[reader.ReadInt32()];
                for (int i = 0; i < Ingredients.Length; i++)
                {
                    Ingredients[i] = new IDGRecipeLoader.WorkStationIngredient();
                    Ingredients[i].FromBytes(reader, resolver);
                    Ingredients[i].Resolve(resolver, Code.ToString() + " (FromBytes)");
                }
                Output = new JsonItemStack();
                Output.FromBytes(reader, resolver.ClassRegistry);
                Output.Resolve(resolver, Code.ToString() + " (FromBytes)", printWarningOnError: true);
                ReturnStack = new JsonItemStack();
                ReturnStack.FromBytes(reader, resolver.ClassRegistry);
                ReturnStack.Resolve(resolver, Code.ToString() + " (FromBytes)", printWarningOnError: true);
            }

            public BasicWorkstationRecipe Clone()
            {
                IDGRecipeLoader.WorkStationIngredient[] ingredients = new IDGRecipeLoader.WorkStationIngredient[Ingredients.Length];
                for (int i = 0; i < Ingredients.Length; i++) ingredients[i] = Ingredients[i].Clone();
                return new BasicWorkstationRecipe()
                {
                    Output = Output.Clone(),
                    ReturnStack = ReturnStack.Clone(),
                    Code = Code,
                    IngredientMaterial = IngredientMaterial,
                    IngredientResistance = IngredientResistance,
                    BaseToolDmg = BaseToolDmg,
                    ToolMode = ToolMode,
                    RequiredWorkstation = RequiredWorkstation,
                    Animation = Animation,
                    Sound = Sound,
                    Enabled = Enabled,
                    Name = Name,
                    Ingredients = ingredients
                };
            }

            public Dictionary<string, string[]> GetNameToCodeMapping(IWorldAccessor world)
            {
                Dictionary<string, string[]> mappings = new Dictionary<string, string[]>();
                if (Ingredients == null || Ingredients.Length == 0) return mappings;
                foreach (var ingreds in Ingredients)
                {
                    if (ingreds.Inputs.Length <= 0) continue;
                    CraftingRecipeIngredient ingred = ingreds.Inputs[0];
                    if (ingred == null || !ingred.Code.Path.Contains("*") || ingred.Name == null) continue;
                    int wildcardStartLen = ingred.Code.Path.IndexOf("*");
                    int wildcardEndLen = ingred.Code.Path.Length - wildcardStartLen - 1;
                    List<string> codes = new List<string>();
                    if (ingred.Type == EnumItemClass.Block)
                    {
                        for (int i = 0; i < world.Blocks.Count; i++)
                        {
                            if (world.Blocks[i].Code == null || world.Blocks[i].IsMissing) continue;
                            if (WildcardUtil.Match(ingred.Code, world.Blocks[i].Code))
                            {
                                string code = world.Blocks[i].Code.Path.Substring(wildcardStartLen);
                                string codepart = code.Substring(0, code.Length - wildcardEndLen);
                                if (ingred.AllowedVariants == null || ingred.AllowedVariants.Contains<string>(codepart))
                                    codes.Add(codepart);
                            }
                        }
                    }
                    else
                    {
                        for (int i = 0; i < world.Items.Count; i++)
                        {
                            if (world.Items[i].Code == null || world.Items[i].IsMissing) continue;
                            if (WildcardUtil.Match(ingred.Code, world.Items[i].Code))
                            {
                                string code = world.Items[i].Code.Path.Substring(wildcardStartLen);
                                string codepart = code.Substring(0, code.Length - wildcardEndLen);
                                if (ingred.AllowedVariants == null || ingred.AllowedVariants.Contains<string>(codepart))
                                    codes.Add(codepart);
                            }
                        }
                    }
                    mappings[ingred.Name] = codes.ToArray();
                }
                return mappings;
            }
        }

        public class BasicWorkstationRecipe : WorkstationRecipe
        {
            public new BasicWorkstationRecipe Clone()
            {
                IDGRecipeLoader.WorkStationIngredient[] ingredients = new IDGRecipeLoader.WorkStationIngredient[Ingredients.Length];
                for (int i = 0; i < Ingredients.Length; i++) ingredients[i] = Ingredients[i].Clone();
                return new BasicWorkstationRecipe()
                {
                    Output = Output.Clone(),
                    ReturnStack = ReturnStack.Clone(),
                    Code = Code,
                    IngredientMaterial = IngredientMaterial,
                    IngredientResistance = IngredientResistance,
                    BaseToolDmg = BaseToolDmg,
                    ToolMode = ToolMode,
                    RequiredWorkstation = RequiredWorkstation,
                    Animation = Animation,
                    Sound = Sound,
                    Enabled = Enabled,
                    Name = Name,
                    Ingredients = ingredients
                };
            }

            public new void ToBytes(BinaryWriter writer)
            {
                writer.Write(Code);
                writer.Write(BaseToolDmg);
                writer.Write(ToolMode);
                writer.Write(RequiredWorkstation);
                writer.Write(Animation);
                writer.Write(Sound);
                writer.Write(IngredientMaterial);
                writer.Write(IngredientResistance);
                writer.Write(Ingredients.Length);
                for (int i = 0; i < Ingredients.Length; i++) Ingredients[i].ToBytes(writer);
                Output.ToBytes(writer);
                ReturnStack.ToBytes(writer);
            }

            public new void FromBytes(BinaryReader reader, IWorldAccessor resolver)
            {
                Code = reader.ReadString();
                BaseToolDmg = reader.ReadInt32();
                ToolMode = reader.ReadString();
                RequiredWorkstation = reader.ReadString();
                Animation = reader.ReadString();
                Sound = reader.ReadString();
                IngredientMaterial = reader.ReadInt32();
                IngredientResistance = reader.ReadDouble();
                Ingredients = new IDGRecipeLoader.WorkStationIngredient[reader.ReadInt32()];
                for (int i = 0; i < Ingredients.Length; i++)
                {
                    Ingredients[i] = new IDGRecipeLoader.WorkStationIngredient();
                    Ingredients[i].FromBytes(reader, resolver);
                    Ingredients[i].Resolve(resolver, Code.ToString() + " (FromBytes)");
                }
                Output = new JsonItemStack();
                Output.FromBytes(reader, resolver.ClassRegistry);
                Output.Resolve(resolver, Code.ToString() + " (FromBytes)", printWarningOnError: true);
                ReturnStack = new JsonItemStack();
                ReturnStack.FromBytes(reader, resolver.ClassRegistry);
                ReturnStack.Resolve(resolver, Code.ToString() + " (FromBytes)", printWarningOnError: true);
            }

            public new Dictionary<string, string[]> GetNameToCodeMapping(IWorldAccessor world)
            {
                Dictionary<string, string[]> mappings = new Dictionary<string, string[]>();
                if (Ingredients == null || Ingredients.Length == 0) return mappings;
                foreach (var ingreds in Ingredients)
                {
                    if (ingreds.Inputs.Length <= 0) continue;
                    CraftingRecipeIngredient ingred = ingreds.Inputs[0];
                    if (ingred == null || !ingred.Code.Path.Contains("*") || ingred.Name == null) continue;
                    int wildcardStartLen = ingred.Code.Path.IndexOf("*");
                    int wildcardEndLen = ingred.Code.Path.Length - wildcardStartLen - 1;
                    List<string> codes = new List<string>();
                    if (ingred.Type == EnumItemClass.Block)
                    {
                        for (int i = 0; i < world.Blocks.Count; i++)
                        {
                            if (world.Blocks[i].Code == null || world.Blocks[i].IsMissing) continue;
                            if (WildcardUtil.Match(ingred.Code, world.Blocks[i].Code))
                            {
                                string code = world.Blocks[i].Code.Path.Substring(wildcardStartLen);
                                string codepart = code.Substring(0, code.Length - wildcardEndLen);
                                if (ingred.AllowedVariants == null || ingred.AllowedVariants.Contains<string>(codepart))
                                    codes.Add(codepart);
                            }
                        }
                    }
                    else
                    {
                        for (int i = 0; i < world.Items.Count; i++)
                        {
                            if (world.Items[i].Code == null || world.Items[i].IsMissing) continue;
                            if (WildcardUtil.Match(ingred.Code, world.Items[i].Code))
                            {
                                string code = world.Items[i].Code.Path.Substring(wildcardStartLen);
                                string codepart = code.Substring(0, code.Length - wildcardEndLen);
                                if (ingred.AllowedVariants == null || ingred.AllowedVariants.Contains<string>(codepart))
                                    codes.Add(codepart);
                            }
                        }
                    }
                    mappings[ingred.Name] = codes.ToArray();
                }
                return mappings;
            }
        }

        public class ComplexWorkstationRecipe : WorkstationRecipe
        {
            public new string Code = "SplitterRecipe";
            public new string Animation = "axesplit-fp";
            public override string ToolMode { get; set; } = "pounding";
            public override string RequiredWorkstation { get; set; } = "logsplitter";
            public new string Sound { get; set; } = "sounds/block/chop2";
            public string ProcessModifier { get; set; } = "splitterblade-single";

            public new void ToBytes(BinaryWriter writer)
            {
                writer.Write(Code);
                writer.Write(BaseToolDmg);
                writer.Write(ToolMode);
                writer.Write(RequiredWorkstation);
                writer.Write(Animation);
                writer.Write(Sound);
                writer.Write(ProcessModifier);
                writer.Write(IngredientMaterial);
                writer.Write(IngredientResistance);
                writer.Write(Ingredients.Length);
                for (int i = 0; i < Ingredients.Length; i++) Ingredients[i].ToBytes(writer);
                Output.ToBytes(writer);
                ReturnStack.ToBytes(writer);
            }

            public new void FromBytes(BinaryReader reader, IWorldAccessor resolver)
            {
                Code = reader.ReadString();
                BaseToolDmg = reader.ReadInt32();
                ToolMode = reader.ReadString();
                RequiredWorkstation = reader.ReadString();
                Animation = reader.ReadString();
                Sound = reader.ReadString();
                ProcessModifier = reader.ReadString();
                IngredientMaterial = reader.ReadInt32();
                IngredientResistance = reader.ReadDouble();
                Ingredients = new IDGRecipeLoader.WorkStationIngredient[reader.ReadInt32()];
                for (int i = 0; i < Ingredients.Length; i++)
                {
                    Ingredients[i] = new IDGRecipeLoader.WorkStationIngredient();
                    Ingredients[i].FromBytes(reader, resolver);
                    Ingredients[i].Resolve(resolver, RequiredWorkstation + " Recipe (FromBytes)");
                }
                Output = new JsonItemStack();
                Output.FromBytes(reader, resolver.ClassRegistry);
                Output.Resolve(resolver, RequiredWorkstation + " Recipe (FromBytes)", printWarningOnError: true);
                ReturnStack = new JsonItemStack();
                ReturnStack.FromBytes(reader, resolver.ClassRegistry);
                ReturnStack.Resolve(resolver, RequiredWorkstation + " Recipe (FromBytes)", printWarningOnError: true);
            }

            public new bool Matches(IWorldAccessor worldForResolve, ItemSlot inputSlots)
            {
                var matched = pairInput(inputSlots);
                if (matched == null) return false;
                return getOutputSize(matched) >= 0;
            }

            public new ComplexWorkstationRecipe Clone()
            {
                IDGRecipeLoader.WorkStationIngredient[] ingredients = new IDGRecipeLoader.WorkStationIngredient[Ingredients.Length];
                for (int i = 0; i < Ingredients.Length; i++) ingredients[i] = Ingredients[i].Clone();
                return new ComplexWorkstationRecipe()
                {
                    Output = Output.Clone(),
                    ReturnStack = ReturnStack.Clone(),
                    Code = Code,
                    IngredientMaterial = IngredientMaterial,
                    IngredientResistance = IngredientResistance,
                    BaseToolDmg = BaseToolDmg,
                    ToolMode = ToolMode,
                    RequiredWorkstation = RequiredWorkstation,
                    Animation = Animation,
                    Sound = Sound,
                    ProcessModifier = ProcessModifier,
                    Enabled = Enabled,
                    Name = Name,
                    Ingredients = ingredients
                };
            }
        }

        public class GroundRecipe : IByteSerializable
        {
            public string Code = "groundRecipe";
            public AssetLocation Name { get; set; }
            public bool Enabled { get; set; } = true;
            public string ToolMode = "chopping";
            public string Animation = "axesplit-fp";
            public string Sound { get; set; } = "sounds/block/chop2";
            public int BaseToolDmg { get; set; } = 1;

            // Fix applied here for nested ground ingredient type
            public IDGRecipeLoader.GroundIngredient[] Ingredients;

            public JsonItemStack Output = new JsonItemStack { Code = new AssetLocation("air"), Type = EnumItemClass.Block, Quantity = 0 };

            public JsonItemStack ReturnStack = new JsonItemStack() { Code = new AssetLocation("air"), Type = EnumItemClass.Block, Quantity = 0 };

            public ItemStack TryCraftNow(ICoreAPI api, ItemSlot inputslots)
            {
                var matched = pairInput(inputslots);
                ItemStack tempstack = Output.ResolvedItemStack.Clone();
                tempstack.StackSize = getOutputSize(matched);
                if (tempstack.StackSize <= 0) return null;
                foreach (var val in matched)
                {
                    val.Key.TakeOut(val.Value.Quantity * (tempstack.StackSize / Output.StackSize));
                    val.Key.MarkDirty();
                }
                return tempstack;
            }

            public bool Matches(IWorldAccessor worldForResolve, ItemSlot inputSlots)
            {
                var matched = pairInput(inputSlots);
                if (matched == null) return false;
                return getOutputSize(matched) >= 0;
            }

            private List<KeyValuePair<ItemSlot, CraftingRecipeIngredient>> pairInput(ItemSlot inputStacks)
            {
                List<int> alreadyFound = new List<int>();
                Queue<ItemSlot> inputSlotsList = new Queue<ItemSlot>();
                if (!inputStacks.Empty) inputSlotsList.Enqueue(inputStacks);
                if (inputSlotsList.Count != Ingredients.Length) return null;

                List<KeyValuePair<ItemSlot, CraftingRecipeIngredient>> matched = new List<KeyValuePair<ItemSlot, CraftingRecipeIngredient>>();
                while (inputSlotsList.Count > 0)
                {
                    ItemSlot inputSlot = inputSlotsList.Dequeue();
                    bool found = false;
                    for (int i = 0; i < Ingredients.Length; i++)
                    {
                        CraftingRecipeIngredient ingred = Ingredients[i].GetMatch(inputSlot.Itemstack);
                        if (ingred != null && !alreadyFound.Contains(i))
                        {
                            matched.Add(new KeyValuePair<ItemSlot, CraftingRecipeIngredient>(inputSlot, ingred));
                            alreadyFound.Add(i);
                            found = true;
                            break;
                        }
                    }
                    if (!found) return null;
                }
                if (matched.Count != Ingredients.Length) return null;
                return matched;
            }

            private int getOutputSize(List<KeyValuePair<ItemSlot, CraftingRecipeIngredient>> matched)
            {
                int outQuantityMul = -1;
                foreach (var val in matched)
                {
                    int posChange = val.Key.StackSize / val.Value.Quantity;
                    if (posChange < outQuantityMul || outQuantityMul == -1) outQuantityMul = posChange;
                }
                if (outQuantityMul == -1) return -1;
                foreach (var val in matched)
                    if (val.Key.StackSize < val.Value.Quantity * outQuantityMul) return -1;
                outQuantityMul = 1;
                return outQuantityMul;
            }

            public string GetOutputName()
            {
                return Lang.Get("indappledgroves:Will make {0}", Output.ResolvedItemStack.GetName());
            }

            public bool Resolve(IWorldAccessor world, string sourceForErrorLogging)
            {
                bool ok = true;
                for (int i = 0; i < Ingredients.Length; i++)
                    ok &= Ingredients[i].Resolve(world, sourceForErrorLogging);
                ok &= Output.Resolve(world, sourceForErrorLogging, printWarningOnError: true);
                if (ReturnStack.Quantity == 0 && ReturnStack.Code.ToString() != "air") ReturnStack.Quantity = 1;
                return ok & ReturnStack.Resolve(world, sourceForErrorLogging, printWarningOnError: true);
            }

            public void ToBytes(BinaryWriter writer)
            {
                writer.Write(Code);
                writer.Write(ToolMode);
                writer.Write(Animation);
                writer.Write(Sound);
                writer.Write(BaseToolDmg);
                writer.Write(Ingredients.Length);
                for (int i = 0; i < Ingredients.Length; i++) Ingredients[i].ToBytes(writer);
                Output.ToBytes(writer);
                ReturnStack.ToBytes(writer);
            }

            public void FromBytes(BinaryReader reader, IWorldAccessor resolver)
            {
                Code = reader.ReadString();
                ToolMode = reader.ReadString();
                Animation = reader.ReadString();
                Sound = reader.ReadString();
                BaseToolDmg = reader.ReadInt32();
                Ingredients = new IDGRecipeLoader.GroundIngredient[reader.ReadInt32()];
                for (int i = 0; i < Ingredients.Length; i++)
                {
                    Ingredients[i] = new IDGRecipeLoader.GroundIngredient();
                    Ingredients[i].FromBytes(reader, resolver);
                    Ingredients[i].Resolve(resolver, "Ground Recipe (FromBytes)");
                }
                Output.FromBytes(reader, resolver.ClassRegistry);
                Output.Resolve(resolver, "Ground Recipe (FromBytes)", printWarningOnError: true);
                ReturnStack = new JsonItemStack();
                ReturnStack.FromBytes(reader, resolver.ClassRegistry);
                ReturnStack.Resolve(resolver, "Ground Recipe Return Stack Not Resolved", printWarningOnError: true);
            }

            public GroundRecipe Clone()
            {
                IDGRecipeLoader.GroundIngredient[] ingredients = new IDGRecipeLoader.GroundIngredient[Ingredients.Length];
                for (int i = 0; i < Ingredients.Length; i++) ingredients[i] = Ingredients[i].Clone();
                return new GroundRecipe()
                {
                    Enabled = Enabled,
                    Name = Name,
                    Code = Code,
                    ToolMode = ToolMode,
                    BaseToolDmg = BaseToolDmg,
                    Animation = Animation,
                    Sound = Sound,
                    Ingredients = ingredients,
                    Output = Output.Clone(),
                    ReturnStack = ReturnStack.Clone(),
                };
            }

            public Dictionary<string, string[]> GetNameToCodeMapping(IWorldAccessor world)
            {
                Dictionary<string, string[]> mappings = new Dictionary<string, string[]>();
                if (Ingredients == null || Ingredients.Length == 0) return mappings;
                foreach (var ingreds in Ingredients)
                {
                    if (ingreds.Inputs.Length <= 0) continue;
                    CraftingRecipeIngredient ingred = ingreds.Inputs[0];
                    if (ingred == null || !ingred.Code.Path.Contains("*") || ingred.Name == null) continue;
                    int wildcardStartLen = ingred.Code.Path.IndexOf("*");
                    int wildcardEndLen = ingred.Code.Path.Length - wildcardStartLen - 1;
                    List<string> codes = new List<string>();
                    if (ingred.Type == EnumItemClass.Block)
                    {
                        for (int i = 0; i < world.Blocks.Count; i++)
                        {
                            if (world.Blocks[i].Code == null || world.Blocks[i].IsMissing) continue;
                            if (WildcardUtil.Match(ingred.Code, world.Blocks[i].Code))
                            {
                                string code = world.Blocks[i].Code.Path.Substring(wildcardStartLen);
                                string codepart = code.Substring(0, code.Length - wildcardEndLen);
                                if (ingred.AllowedVariants == null || ingred.AllowedVariants.Contains<string>(codepart))
                                    codes.Add(codepart);
                            }
                        }
                    }
                    else
                    {
                        for (int i = 0; i < world.Items.Count; i++)
                        {
                            if (world.Items[i].Code == null || world.Items[i].IsMissing) continue;
                            if (WildcardUtil.Match(ingred.Code, world.Items[i].Code))
                            {
                                string code = world.Items[i].Code.Path.Substring(wildcardStartLen);
                                string codepart = code.Substring(0, code.Length - wildcardEndLen);
                                if (ingred.AllowedVariants == null || ingred.AllowedVariants.Contains<string>(codepart))
                                    codes.Add(codepart);
                            }
                        }
                    }
                    mappings[ingred.Name] = codes.ToArray();
                }
                return mappings;
            }
        }
    }
}
