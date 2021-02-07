using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Mutagen.Bethesda;
using Mutagen.Bethesda.Skyrim;
using Mutagen.Bethesda.Synthesis;
using Noggog;

namespace UniquePlayer
{
    public class Program
    {
        public static async Task<int> Main(string[] args)
        {
            return await SynthesisPipeline.Instance
              .AddPatch<ISkyrimMod, ISkyrimModGetter>(RunPatch)
              .Run(args, new RunPreferences()
              {
                  ActionsForEmptyArgs = new RunDefaultPatcher()
                  {
                      IdentifyingModKey = "UniquePlayer.esp",
                      TargetRelease = GameRelease.SkyrimSE,
                  }
              });
        }

        public static ModKey raceCompatibilityEsm = new ModKey("RaceCompatibility", ModType.Master);

        public static ModKey skyrimEsm = new ModKey("Skyrim", ModType.Master);

        public static Dictionary<FormKey, FormKey> vanillaRaceToActorProxyKeywords = new Dictionary<FormKey, FormKey>{
      { new FormKey(skyrimEsm, 0x013740), new FormKey(raceCompatibilityEsm, 0x001D8B) }, // Argonian
      { new FormKey(skyrimEsm, 0x013741), new FormKey(raceCompatibilityEsm, 0x001D8A) }, // Breton
      { new FormKey(skyrimEsm, 0x013742), new FormKey(raceCompatibilityEsm, 0x001D8F) }, // DarkElf
      { new FormKey(skyrimEsm, 0x013743), new FormKey(raceCompatibilityEsm, 0x001D8E) }, // HighElf
      { new FormKey(skyrimEsm, 0x013744), new FormKey(raceCompatibilityEsm, 0x001D90) }, // Imperial
      { new FormKey(skyrimEsm, 0x013745), new FormKey(raceCompatibilityEsm, 0x001D8C) }, // Khajit
      { new FormKey(skyrimEsm, 0x013746), new FormKey(raceCompatibilityEsm, 0x001D93) }, // Nord
      { new FormKey(skyrimEsm, 0x013747), new FormKey(raceCompatibilityEsm, 0x001D8D) }, // Orc
      { new FormKey(skyrimEsm, 0x013748), new FormKey(raceCompatibilityEsm, 0x001D91) }, // Redguard
      { new FormKey(skyrimEsm, 0x013749), new FormKey(raceCompatibilityEsm, 0x001D92) }, // WoodElf
      };

        public static FormKey playableRaceFormListFormKey = new FormKey(raceCompatibilityEsm, 0x000D62);

        public static FormKey playableVampireRaceFormListFormKey = new FormKey(raceCompatibilityEsm, 0x000D63);

        public static void RunPatch(IPatcherState<ISkyrimMod, ISkyrimModGetter> state)
        {
            var linkCache = state.LinkCache;

            var playableRaceFormList = linkCache.Resolve<IFormListGetter>(playableRaceFormListFormKey);
            var playableVampireRaceFormList = linkCache.Resolve<IFormListGetter>(playableVampireRaceFormListFormKey);

            var playableRaceFormLinks = playableRaceFormList.ContainedFormLinks;
            var playableVampireRaceFormLinks = playableVampireRaceFormList.ContainedFormLinks;

            if (playableRaceFormLinks.Count() != playableVampireRaceFormLinks.Count())
            {
                throw new Exception("The number of playable races and the number of playable vampire races does not match, cannot proceed.");
            }

            var victimRaceFormKeys = playableRaceFormLinks.Select(x => x.FormKey).Concat(playableVampireRaceFormLinks.Select(x => x.FormKey)).ToHashSet();

            var otherFormLists = state.LoadOrder.PriorityOrder.WinningOverrides<IFormListGetter>().Where(x => x.FormKey != playableRaceFormListFormKey && x.FormKey != playableVampireRaceFormListFormKey && x.ContainedFormLinks.Any(y => victimRaceFormKeys.Contains(y.FormKey))).Select(x => state.PatchMod.FormLists.GetOrAddAsOverride(x));

            var modifiedPlayableRaceFormList = state.PatchMod.FormLists.GetOrAddAsOverride(playableRaceFormList);
            var modifiedPlayableVampireRaceFormList = state.PatchMod.FormLists.GetOrAddAsOverride(playableVampireRaceFormList);

            var replacementPlayableRacesDict = new Dictionary<FormKey, FormKey>();

            var replacementHeadParts = new Dictionary<FormKey, FormKey>();

            var replacementTexturePathDict = new Dictionary<string, string>();
            var replacementTextureSets = new Dictionary<FormKey, FormKey>();
            var inspectedTexturePaths = new HashSet<string>();

            var replacementMeshPathDict = new Dictionary<string, string>();
            var inspectedMeshPaths = new HashSet<string>();

            var texturesPath = state.Settings.DataFolderPath + "\\Textures\\";
            var meshesPath = state.Settings.DataFolderPath + "\\Meshes\\";

            string? changeTexturePath(string? path, ref bool changed)
            {
                if (path is null) return null;
                if (inspectedTexturePaths.Contains(path)) return path;
                if (replacementTexturePathDict.TryGetValue(path, out var newPath))
                {
                    changed = true;
                    return newPath;
                }

                newPath = "Player\\Textures\\" + path;
                if (File.Exists(texturesPath + newPath))
                {
                    replacementTexturePathDict.Add(path, newPath);
                    changed = true;
                    return newPath;
                }
                inspectedTexturePaths.Add(path);
                return path;
            }

            string changeMeshPath(string path, ref bool changed)
            {
                //if (path is null) return null;
                if (inspectedMeshPaths.Contains(path)) return path;
                if (replacementMeshPathDict.TryGetValue(path, out var newPath))
                {
                    changed = true;
                    return newPath;
                }

                newPath = "Player\\Meshes\\" + path;
                if (File.Exists(meshesPath + newPath))
                {
                    replacementMeshPathDict.Add(path, newPath);
                    changed = true;
                    return newPath;
                }
                inspectedMeshPaths.Add(path);
                return path;
            }

            bool updateTextureSet(IFormLinkNullable<ITextureSetGetter> textureSetFormLink, IMajorRecordCommonGetter rec)
            {
                if (textureSetFormLink.IsNull) return false;
                var textureSetFormKey = textureSetFormLink.FormKey;
                if (replacementTextureSets.ContainsKey(textureSetFormKey)) return true;
                var txst = textureSetFormLink.TryResolve(state.LinkCache).Value;
                if (txst == null) throw RecordException.Factory(new NullReferenceException($"Could not find referenced TXST {textureSetFormLink}"), rec);

                var changed = false;
                changeTexturePath(txst.Diffuse, ref changed);
                changeTexturePath(txst.NormalOrGloss, ref changed);
                changeTexturePath(txst.EnvironmentMaskOrSubsurfaceTint, ref changed);
                changeTexturePath(txst.GlowOrDetailMap, ref changed);
                changeTexturePath(txst.Height, ref changed);
                changeTexturePath(txst.Environment, ref changed);
                changeTexturePath(txst.Multilayer, ref changed);
                changeTexturePath(txst.BacklightMaskOrSpecular, ref changed);

                if (!changed) return false;

                var newTxst = state.PatchMod.TextureSets.AddNew($"UniquePlayer_{txst.EditorID}");
                newTxst.DeepCopyIn(txst, new TextureSet.TranslationMask(defaultOn: true)
                {
                    EditorID = false
                });
                replacementTextureSets.Add(textureSetFormKey, newTxst.FormKey);

                newTxst.Diffuse = changeTexturePath(txst.Diffuse, ref changed);
                newTxst.NormalOrGloss = changeTexturePath(txst.NormalOrGloss, ref changed);
                newTxst.EnvironmentMaskOrSubsurfaceTint = changeTexturePath(txst.EnvironmentMaskOrSubsurfaceTint, ref changed);
                newTxst.GlowOrDetailMap = changeTexturePath(txst.GlowOrDetailMap, ref changed);
                newTxst.Height = changeTexturePath(txst.Height, ref changed);
                newTxst.Environment = changeTexturePath(txst.Environment, ref changed);
                newTxst.Multilayer = changeTexturePath(txst.Multilayer, ref changed);
                newTxst.BacklightMaskOrSpecular = changeTexturePath(txst.BacklightMaskOrSpecular, ref changed);
                return true;
            }

            void commonRaceUpdates(Race race)
            {
                if (race.HeadData == null) return;
                foreach (var headData in race.HeadData)
                {
                    if (headData == null) continue;
                    foreach (var textureSetFormLink in headData.FaceDetails)
                    {
                        updateTextureSet(textureSetFormLink, race);
                    }
                    headData.FaceDetails.RemapLinks(replacementTextureSets);

                    foreach (var headPartItem in headData.HeadParts)
                    {
                        var headPartFormKey = headPartItem.Head.FormKey;
                        if (replacementHeadParts.ContainsKey(headPartFormKey)) continue;
                        var headPart = headPartItem.Head.Resolve(state.LinkCache);
                        if (headPart == null) throw RecordException.Factory(new NullReferenceException($"Could not find referenced HDPT {headPartFormKey}"), race);
                        var txstFormLink = headPart.TextureSet;
                        if (txstFormLink.IsNull) continue;
                        var txstFormKey = txstFormLink.FormKey;
                        var changed = updateTextureSet(txstFormLink, race);
                        if (!changed) continue;
                        var newHeadPart = state.PatchMod.HeadParts.AddNew($"UniquePlayer_{headPart.EditorID}");
                        newHeadPart.DeepCopyIn(headPart, new HeadPart.TranslationMask(defaultOn: true)
                        {
                            EditorID = false
                        });
                        newHeadPart.RemapLinks(replacementTextureSets);
                        replacementHeadParts.Add(headPartFormKey, newHeadPart.FormKey);
                    }
                    headData.HeadParts.RemapLinks(replacementHeadParts);
                }
            }

            Console.WriteLine("Creating new player-only races from existing playable races.");
            foreach (var (raceLink, vampireRaceLink) in Enumerable.Zip<FormLinkInformation, FormLinkInformation>(playableRaceFormLinks, playableVampireRaceFormLinks))
            {
                var raceFormKey = raceLink.FormKey;
                var vampireRaceFormKey = vampireRaceLink.FormKey;

                var race = linkCache.Resolve<IRaceGetter>(raceFormKey);
                var vampireRace = linkCache.Resolve<IRaceGetter>(vampireRaceFormKey);

                if (!race.Flags.HasFlag(Race.Flag.Playable))
                {
                    throw RecordException.Factory(new Exception("Race in PlayableRaceList was not playable"), race);
                }
                var newRace = state.PatchMod.Races.AddNew($"UniquePlayer_{race.EditorID}");
                newRace.DeepCopyIn(race, new Race.TranslationMask(defaultOn: true)
                {
                    EditorID = false
                });

                // add ActorProxy<race> for copies of vanilla races.
                if (vanillaRaceToActorProxyKeywords.TryGetValue(raceFormKey, out var actorProxyKeywordFormKey))
                {
                    (newRace.Keywords ??= new ExtendedList<IFormLink<IKeywordGetter>>()).Add(actorProxyKeywordFormKey);
                }

                commonRaceUpdates(newRace);

                var newVampireRace = state.PatchMod.Races.AddNew($"UniquePlayer_{vampireRace.EditorID}");
                newVampireRace.DeepCopyIn(vampireRace, new Race.TranslationMask(defaultOn: true)
                {
                    EditorID = false
                });

                commonRaceUpdates(newVampireRace);

                // clear playable flag on old race.
                var modifiedRace = state.PatchMod.Races.GetOrAddAsOverride(race);
                modifiedRace.Flags ^= Race.Flag.Playable;

                replacementPlayableRacesDict.Add(raceFormKey, newRace.FormKey);
                replacementPlayableRacesDict.Add(vampireRaceFormKey, newVampireRace.FormKey);

                foreach (var otherFormList in otherFormLists)
                {
                    var formLinks = otherFormList.ContainedFormLinks;
                    if (formLinks.Any(x => x.FormKey == raceFormKey))
                    {
                        otherFormList.Items.Add(newRace);
                    }
                    if (formLinks.Any(x => x.FormKey == vampireRaceFormKey))
                    {
                        otherFormList.Items.Add(newVampireRace);
                    }
                }
            }

            Console.WriteLine("Replacing the list of playable races (as supported by RaceCompatability.esm) with the newly created races.");
            modifiedPlayableRaceFormList.RemapLinks(replacementPlayableRacesDict);
            modifiedPlayableVampireRaceFormList.RemapLinks(replacementPlayableRacesDict);

            Console.WriteLine("Changing Player's race to our newly created player-only Race");
            state.PatchMod.Npcs.GetOrAddAsOverride(linkCache.Resolve<INpcGetter>(new FormKey(skyrimEsm, 0x000007))).RemapLinks(replacementPlayableRacesDict);

            Console.WriteLine("Creating new ArmorAddons that use player-specific meshes or editing existing ArmorAddons to support newly added races.");

            var armorAddonAdditions = new Dictionary<FormKey, FormKey>();

            foreach (var armorAddon in state.LoadOrder.PriorityOrder.WinningOverrides<IArmorAddonGetter>().Where(x => victimRaceFormKeys.Contains(x.Race.FormKey) || x.AdditionalRaces.Any(y => victimRaceFormKeys.Contains(y.FormKey))))
            {
                bool needsEdit = false;
                var races = armorAddon.AdditionalRaces.Select(x => x.FormKey)
                  .Append(armorAddon.Race.FormKey);
                var replacementRaces = races.Where(x => victimRaceFormKeys.Contains(x)).Select(x => replacementPlayableRacesDict[x]);

                void modelNeedsEdit(IModelGetter? model)
                {
                    if (model is null) return;
                    changeMeshPath(model.File, ref needsEdit);
                    if (model.AlternateTextures != null)
                    {
                        foreach (var AlternateTexture in model.AlternateTextures)
                        {
                            needsEdit |= updateTextureSet(AlternateTexture.NewTexture, armorAddon);
                        }
                    }
                }

                void applyModelEdit(Model? model)
                {
                    if (model is null) return;
                    model.File = changeMeshPath(model.File, ref needsEdit);
                    // model.AlternateTextures covered by running RemapLinks at the top level.
                }

                armorAddon.FirstPersonModel?.ForEach(modelNeedsEdit);
                armorAddon.WorldModel?.ForEach(modelNeedsEdit);

                armorAddon.SkinTexture?.ForEach(x =>
                {
                    if (!x.IsNull) needsEdit |= updateTextureSet(x, armorAddon);
                });


                if (needsEdit)
                {
                    var newArmorAddon = state.PatchMod.ArmorAddons.AddNew($"UniquePlayer_{armorAddon.EditorID}");
                    newArmorAddon.DeepCopyIn(armorAddon, new ArmorAddon.TranslationMask(defaultOn: true)
                    {
                        EditorID = false,
                        Race = false,
                        AdditionalRaces = false
                    });
                    newArmorAddon.Race = new FormLinkNullable<IRaceGetter>(replacementRaces.Take(1).Single());
                    var additionalRaces = newArmorAddon.AdditionalRaces;
                    replacementRaces.Skip(1).ForEach(x => additionalRaces.Add(new FormLink<IRaceGetter>(x)));

                    newArmorAddon.FirstPersonModel?.ForEach(applyModelEdit);
                    newArmorAddon.WorldModel?.ForEach(applyModelEdit);

                    newArmorAddon.RemapLinks(replacementTextureSets);

                    armorAddonAdditions.Add(armorAddon.FormKey, newArmorAddon.FormKey);
                }
                else
                {
                    var modifiedArmorAddon = state.PatchMod.ArmorAddons.GetOrAddAsOverride(armorAddon);
                    var additionalRaces = modifiedArmorAddon.AdditionalRaces;
                    replacementRaces.ForEach(x => additionalRaces.Add(new FormLink<IRaceGetter>(x)));
                }
            }

            Console.WriteLine("Registering new ArmorAddons with Armors");
            foreach (var armor in state.LoadOrder.PriorityOrder.WinningOverrides<IArmorGetter>().Where(x => x.Armature.Any(y => armorAddonAdditions.ContainsKey(y.FormKey))).Select(x => state.PatchMod.Armors.GetOrAddAsOverride(x)))
            {
                foreach (var item in armor.Armature.ToList())
                {
                    if (armorAddonAdditions.TryGetValue(item.FormKey, out var value))
                        armor.Armature.Add(new FormLink<IArmorAddon>(value));
                }
            }
        }
    }
}
