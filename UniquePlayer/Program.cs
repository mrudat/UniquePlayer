using Mutagen.Bethesda;
using Mutagen.Bethesda.FormKeys.SkyrimSE;
using Mutagen.Bethesda.Skyrim;
using Mutagen.Bethesda.Synthesis;
using Noggog;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.IO.Abstractions;
using System.Linq;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace UniquePlayer
{
    public class Program
    {
        private readonly IPatcherState<ISkyrimMod, ISkyrimModGetter> State;

        private readonly ISkyrimMod PatchMod;

        private readonly ILinkCache<ISkyrimMod, ISkyrimModGetter> LinkCache;

        private readonly LoadOrder<IModListing<ISkyrimModGetter>> LoadOrder;

        private readonly IFileSystem _fileSystem;

        private readonly TexturePaths TexturePaths;

        private readonly MeshPaths MeshPaths;

        private readonly TextureSets TextureSets;

        private readonly HeadParts HeadParts;

        static Lazy<Settings> Settings = null!;

        private static readonly Dictionary<string, string> BodyReferenceToBodyName = new()
        {
            { "CBBE Hands", "CBBE" },
            { "CBBE Body", "CBBE" },
            { "CBBE Feet", "CBBE" },
            { "TBD - Reference - Default Body", "TBD" },
        };

        public Program(IPatcherState<ISkyrimMod, ISkyrimModGetter> state, IFileSystem? fileSystem = null)
        {
            State = state;
            LinkCache = state.LinkCache;
            PatchMod = state.PatchMod;
            LoadOrder = state.LoadOrder;

            _fileSystem = fileSystem ?? new FileSystem();

            TexturePaths = new TexturePaths(_fileSystem);
            MeshPaths = new MeshPaths(_fileSystem);
            TextureSets = new TextureSets(PatchMod, LinkCache, texturePaths: TexturePaths, fileSystem: _fileSystem);
            HeadParts = new HeadParts(PatchMod, LinkCache, TextureSets, MeshPaths, _fileSystem);
        }

        public static async Task<int> Main(string[] args)
        {
            return await SynthesisPipeline.Instance
                .AddRunnabilityCheck(RunnabilityCheck)
                .AddPatch<ISkyrimMod, ISkyrimModGetter>(RunPatch)
                .SetAutogeneratedSettings(
                    nickname: "Settings",
                    path: "settings.json",
                    out Settings)
                .SetTypicalOpen(GameRelease.SkyrimSE, "UniquePlayer.esp")
                .Run(args);
        }

        public static void RunnabilityCheck(IRunnabilityState state)
        {
            new RunnabilityCheck(state, Settings.Value).Check();
        }

        public static void RunPatch(IPatcherState<ISkyrimMod, ISkyrimModGetter> state)
        {
            new Program(state).RunPatch();
        }

        public static void BodySlidePaths(string dataPath, string? bodySlidePath, out string outfitsPath, out string groupsPath)
        {
            bodySlidePath ??= Path.Join(dataPath, "CalienteTools", "BodySlide");
            outfitsPath = Path.Join(bodySlidePath, "SliderSets");
            groupsPath = Path.Join(bodySlidePath, "SliderGroups");
        }

        public static readonly Dictionary<IFormLinkGetter<IRaceGetter>, IFormLinkGetter<IKeywordGetter>> vanillaRaceToActorProxyKeywords = new()
        {
            { Skyrim.Race.ArgonianRace, RaceCompatibility.Keyword.ActorProxyArgonian },
            { Skyrim.Race.BretonRace, RaceCompatibility.Keyword.ActorProxyBreton },
            { Skyrim.Race.DarkElfRace, RaceCompatibility.Keyword.ActorProxyDarkElf },
            { Skyrim.Race.HighElfRace, RaceCompatibility.Keyword.ActorProxyHighElf },
            { Skyrim.Race.ImperialRace, RaceCompatibility.Keyword.ActorProxyImperial },
            { Skyrim.Race.KhajiitRace, RaceCompatibility.Keyword.ActorProxyKhajiit },
            { Skyrim.Race.NordRace, RaceCompatibility.Keyword.ActorProxyNord },
            { Skyrim.Race.OrcRace, RaceCompatibility.Keyword.ActorProxyOrc },
            { Skyrim.Race.RedguardRace, RaceCompatibility.Keyword.ActorProxyRedguard },
            { Skyrim.Race.WoodElfRace, RaceCompatibility.Keyword.ActorProxyWoodElf },
        };

        public readonly Dictionary<FormKey, FormKey> replacementPlayableRacesDict = new();

        public readonly HashSet<IFormLinkGetter<INpcGetter>> presetCharacters = new();

        public readonly HashSet<string> seenDirectories = new();

        public static readonly string[] meshSuffixesWithTriFiles = {
            "_1.nif",
            ".nif"
        };

        public void CopyAndModifyOutfitFiles(string dataPath)
        {
            BodySlidePaths(dataPath, Settings.Value.CustomBodyslideInstallPath ? Settings.Value.BodySlideInstallPath : null, out string outfitsPath, out string groupsPath);

            var outfitOutputFileName = "\\UniquePlayer.osp";
            var groupOutputFileName = "\\UniquePlayer.xml";

            var oldToNewOutfitNames = new Dictionary<string, string>();

            static XDocument? tryLoad(string path)
            {
                try
                {
                    return XDocument.Load(path, LoadOptions.PreserveWhitespace);
                }
                catch (Exception e)
                {
                    Console.WriteLine(e);
                    return null;
                }
            }

            var outfitsData = (
                from filePath in _fileSystem.Directory.GetFiles(outfitsPath).AsParallel()
                where filePath.EndsWith(".osp")
&& !filePath.EndsWith(outfitOutputFileName)
                where _fileSystem.File.Exists(filePath)
                let doc = tryLoad(filePath)
                where doc is not null
                let sliderSets = doc.Element("SliderSetInfo")?.Elements("SliderSet")
                where sliderSets is not null
                from sliderSet in sliderSets
                let name = sliderSet.GetAttribute("name")
                where name is not null
                group sliderSet by name
            ).ToDictionary(x => x.Key, x => x.First());

            var outfitsDoc = new XDocument(
                new XDeclaration("1.0", "utf-8", "yes")
            );
            var sliderSetInfo = new XElement("SliderSetInfo", new XAttribute("version", "1"));
            outfitsDoc.Add(sliderSetInfo);

            Dictionary<string, HashSet<string>> uniquePlayerOutfits = new();

            var bodyNameToSliderNameSets = new Dictionary<string, ImmutableHashSet<string>>();

            foreach (var (outfitName, bodyName) in BodyReferenceToBodyName)
            {
                if (!outfitsData.TryGetValue(outfitName, out var outfitData)) continue;

                bodyNameToSliderNameSets[bodyName] = outfitData
                    .Elements("Slider")
                    .Select(e => e.GetAttribute("name"))
                    .NotNull()
                    .ToImmutableHashSet();
            }

            var outfitNameToBodyName = new Dictionary<string, string>();

            foreach (var (oldOutfitName, outfitData) in outfitsData)
            {
                var outfitPathElement = outfitData.Element("OutputPath");
                if (outfitPathElement is null) continue;
                outfitPathElement.Value = MeshPaths.MangleMeshesPath(outfitPathElement.Value, "Player", out _);

                if (!BodyReferenceToBodyName.TryGetValue(oldOutfitName, out var bodyName))
                {
                    var sliderNames = outfitData
                        .Elements("Slider")
                        .Select(e => e.GetAttribute("name"))
                        .NotNull()
                        .ToImmutableHashSet();

                    var highestCount = 0;
                    bodyName = "unknown";

                    foreach (var (candidateBodyName, candidateSliderNames) in bodyNameToSliderNameSets)
                    {
                        var matchCount = sliderNames.CountIntersect(candidateSliderNames);

                        if (matchCount > highestCount)
                        {
                            highestCount = matchCount;
                            bodyName = candidateBodyName;
                        }
                    }
                }

                var hasSMP = outfitData
                    .Elements("Shape")?
                    .Select(x => x.GetAttribute("target"))
                    .NotNull()
                    .Any(x => x.StartsWith("Virtual")) == true;

                var bodyNamePlusSMP = bodyName;

                if (hasSMP)
                    bodyNamePlusSMP += "-SMP";

                outfitNameToBodyName[oldOutfitName] = bodyNamePlusSMP;

                var newOutfitName = oldOutfitName;
                if (!oldOutfitName.Contains(bodyName))
                    newOutfitName += $" ({bodyName})";
                if (hasSMP && !oldOutfitName.Contains("SMP"))
                    newOutfitName += $" (SMP)";
                newOutfitName += " (Unique Player)";

                outfitData.Attribute("name")!.Value = newOutfitName;

                sliderSetInfo.Add(outfitData);
                oldToNewOutfitNames[oldOutfitName] = newOutfitName;

                if (!uniquePlayerOutfits.TryGetValue(bodyNamePlusSMP, out var uniquePlayerOutfitsForBodyName))
                    uniquePlayerOutfitsForBodyName = uniquePlayerOutfits[bodyNamePlusSMP] = new();
                uniquePlayerOutfitsForBodyName.Add(newOutfitName);
            }

            var outfitGroups =
                from filePath in _fileSystem.Directory.GetFiles(groupsPath) //.AsParallel()
                where filePath.EndsWith(".xml")
                   && !filePath.EndsWith(groupOutputFileName)
                where _fileSystem.File.Exists(filePath)
                let doc = tryLoad(filePath)
                where doc is not null
                let outfitGroups2 = doc.Element("SliderGroups")?.Elements("Group")
                where outfitGroups2 is not null
                from outfitGroup in outfitGroups2
                let outfitGroupName = outfitGroup.GetAttribute("name")
                where outfitGroupName is not null
                from outfitGroupMember in outfitGroup.Elements("Member")
                where outfitGroupMember is not null
                let outfitName = outfitGroupMember.GetAttribute("name")
                where outfitName is not null
                join rec in outfitNameToBodyName // only includes existing outfit names
                on outfitName equals rec.Key
                let bodyName = rec.Value
                let newOutfitName = oldToNewOutfitNames[outfitName]
                group newOutfitName by (outfitGroupName, bodyName);

            var sliderGroupDoc = new XDocument();
            var sliderGroups = new XElement("SliderGroups");
            sliderGroupDoc.Add(sliderGroups);

            static XElement MakeSliderGroup(IEnumerable<string> outfitNames, string outfitGroupName)
            {
                var sliderGroup = new XElement("Group", new XAttribute("name", outfitGroupName));

                foreach (var outfitName in outfitNames.OrderBy(x => x))
                {
                    sliderGroup.Add(new XElement("Member", new XAttribute("name", outfitName)));
                }

                return sliderGroup;
            }

            foreach (var item in outfitGroups)
            {
                var (outfitGroupName, bodyName) = item.Key;

                if (!outfitGroupName.Contains(bodyName))
                    outfitGroupName += $" ({bodyName})";
                outfitGroupName += " (Unique Player)";

                sliderGroups.Add(MakeSliderGroup(item, outfitGroupName));
            }

            foreach (var item in (from item in outfitNameToBodyName
                                  group item.Key by item.Value))
            {
                sliderGroups.Add(MakeSliderGroup(item, $"Original ({item.Key})"));
            }

            foreach (var (bodyName, uniquePlayerOutfitsForBodyName) in uniquePlayerOutfits)
                sliderGroups.Add(MakeSliderGroup(uniquePlayerOutfitsForBodyName, $"Unique Player ({bodyName})"));

            outfitsDoc.Save(outfitsPath + outfitOutputFileName);
            sliderGroupDoc.Save(groupsPath + groupOutputFileName);
        }

        public void RunPatch()
        {
            var outfitFilesTask = new Task(() => CopyAndModifyOutfitFiles(State.DataFolderPath));

            var playableRaceFormList = RaceCompatibility.FormList.PlayableRaceList.Resolve(LinkCache);
            var playableVampireRaceFormList = RaceCompatibility.FormList.PlayableVampireList.Resolve(LinkCache);

            var playableRaceFormLinks = playableRaceFormList.ContainedFormLinks.Select(x => new FormLink<IRaceGetter>(x.FormKey)).ToList();
            var playableVampireRaceFormLinks = playableVampireRaceFormList.ContainedFormLinks.Select(x => new FormLink<IRaceGetter>(x.FormKey)).ToList();

            if (playableRaceFormLinks.Count != playableVampireRaceFormLinks.Count)
                throw new Exception("The number of playable races and the number of playable vampire races does not match, cannot proceed.");

            outfitFilesTask.Start();

            var victimRaceFormKeys = playableRaceFormLinks.Select(x => x.FormKey.AsLinkGetter<IRaceGetter>()).Concat(playableVampireRaceFormLinks.Select(x => x.FormKey.AsLinkGetter<IRaceGetter>())).ToHashSet();

            var otherFormLists =
                from x in LoadOrder.PriorityOrder.WinningOverrides<IFormListGetter>()
                where !x.Equals(RaceCompatibility.FormList.PlayableRaceList)
                && !x.Equals(RaceCompatibility.FormList.PlayableVampireList)
                && x.ContainedFormLinks.Any(y => victimRaceFormKeys.Contains(y.FormKey.AsLink<IRaceGetter>()))
                select PatchMod.FormLists.GetOrAddAsOverride(x);

            var modifiedPlayableRaceFormList = PatchMod.FormLists.GetOrAddAsOverride(playableRaceFormList);
            var modifiedPlayableVampireRaceFormList = PatchMod.FormLists.GetOrAddAsOverride(playableVampireRaceFormList);

            var texturesPath = State.DataFolderPath + "\\Textures\\";
            var meshesPath = State.DataFolderPath + "\\Meshes\\";

            Console.WriteLine("Creating new player-only races from existing playable races.");
            foreach (var (raceLink, vampireRaceLink) in playableRaceFormLinks.Zip(playableVampireRaceFormLinks))
            {
                var race = raceLink.Resolve(LinkCache);
                var vampireRace = vampireRaceLink.Resolve(LinkCache);

                if (!race.Flags.HasFlag(Race.Flag.Playable))
                    throw RecordException.Factory(new Exception("Race in PlayableRaceList was not playable"), race);

                var newRace = CopyRace(race, texturesPath, meshesPath);

                // add ActorProxy<race> for copies of vanilla races; it's assumed that either the non-vanilla race already has the appropriate ActorProxy<race>, or is happy with no ActorProxy<race>, and our copy should be the same.
                if (vanillaRaceToActorProxyKeywords.TryGetValue(raceLink, out var actorProxyKeywordFormKey))
                    (newRace.Keywords ??= new()).Add(actorProxyKeywordFormKey);

                var newVampireRace = CopyRace(vampireRace, texturesPath, meshesPath);

                var modifiedRace = PatchMod.Races.GetOrAddAsOverride(race);
                modifiedRace.Flags ^= Race.Flag.Playable;
                modifiedRace.HeadData?.ForEach(x => x?.RacePresets.RemoveAll(x => true));

                foreach (var otherFormList in otherFormLists)
                {
                    var formLinks = otherFormList.ContainedFormLinks;
                    if (formLinks.Any(x => x.FormKey == raceLink.FormKey))
                        otherFormList.Items.Add(newRace);
                    if (formLinks.Any(x => x.FormKey == vampireRaceLink.FormKey))
                        otherFormList.Items.Add(newVampireRace);
                }
            }

            Console.WriteLine("Replacing the list of playable races (as supported by RaceCompatability.esm) with the newly created races.");
            modifiedPlayableRaceFormList.RemapLinks(replacementPlayableRacesDict);
            modifiedPlayableVampireRaceFormList.RemapLinks(replacementPlayableRacesDict);

            Console.WriteLine("Changing Player's race to our newly created player-only Race");
            PatchMod.Npcs.GetOrAddAsOverride(Skyrim.Npc.Player.Resolve(LinkCache)).RemapLinks(replacementPlayableRacesDict);

            foreach (var item in presetCharacters)
                PatchMod.Npcs.GetOrAddAsOverride(item.Resolve(LinkCache)).RemapLinks(replacementPlayableRacesDict);

            // TODO only do armor addons that are used by playable armor?
            Console.WriteLine("Creating new ArmorAddons that use player-specific meshes or editing existing ArmorAddons to support newly added races.");

            var armorAddonAdditions = new Dictionary<IFormLinkGetter<IArmorAddonGetter>, IFormLinkGetter<IArmorAddonGetter>>();

            foreach (var armorAddon in LoadOrder.PriorityOrder.WinningOverrides<IArmorAddonGetter>().Where(x => victimRaceFormKeys.Contains(x.Race) || x.AdditionalRaces.Any(y => victimRaceFormKeys.Contains(y))).ToList())
            {
                bool needsEdit = false;
                var races = armorAddon.AdditionalRaces.Append(armorAddon.Race);

                var replacementRaces = races.Where(x => victimRaceFormKeys.Contains(x)).Select(x => replacementPlayableRacesDict[x.FormKey]).ToList();

                void modelNeedsEdit(IModelGetter? model)
                {
                    if (model is null) return;
                    MeshPaths.ChangeMeshPath(model.File, ref needsEdit, meshesPath);
                    model.AlternateTextures?.ForEach(alternateTexture => needsEdit |= TextureSets.UpdateTextureSet(alternateTexture.NewTexture, texturesPath));
                }

                void applyModelEdit(Model? model)
                {
                    if (model is null) return;
                    model.File = MeshPaths.ChangeMeshPath(model.File, ref needsEdit, meshesPath);
                    // model.AlternateTextures covered by running RemapLinks at the top level.
                }

                armorAddon.FirstPersonModel?.ForEach(modelNeedsEdit);
                armorAddon.WorldModel?.ForEach(modelNeedsEdit);

                armorAddon.SkinTexture?.ForEach(x =>
                {
                    if (!x.IsNull) needsEdit |= TextureSets.UpdateTextureSet(x, texturesPath);
                });

                if (needsEdit)
                {
                    var newArmorAddon = PatchMod.ArmorAddons.AddNew($"{armorAddon.EditorID}_UniquePlayer");
                    newArmorAddon.DeepCopyIn(armorAddon, new ArmorAddon.TranslationMask(defaultOn: true)
                    {
                        EditorID = false,
                        Race = false,
                        AdditionalRaces = false
                    });
                    newArmorAddon.Race.SetTo(replacementRaces.First());
                    replacementRaces.Skip(1).ForEach(x => newArmorAddon.AdditionalRaces.Add(x));

                    newArmorAddon.FirstPersonModel?.ForEach(applyModelEdit);
                    newArmorAddon.WorldModel?.ForEach(applyModelEdit);

                    newArmorAddon.RemapLinks(TextureSets.replacementTextureSets);

                    armorAddonAdditions.Add(armorAddon.AsLink(), newArmorAddon.AsLink());

                    // remove defaultRace from base armorAddons?
                    if (races.Contains(Skyrim.Race.DefaultRace) && false)
                    {
                        var modifiedArmorAddon = PatchMod.ArmorAddons.GetOrAddAsOverride(armorAddon);
                        if (modifiedArmorAddon.Race.Equals(Skyrim.Race.DefaultRace))
                        {
                            var firstRace = modifiedArmorAddon.AdditionalRaces.First().FormKey;
                            modifiedArmorAddon.Race.SetTo(firstRace);
                            modifiedArmorAddon.AdditionalRaces.Remove(firstRace);
                        }
                        else
                        {
                            modifiedArmorAddon.AdditionalRaces.Remove(Skyrim.Race.DefaultRace);
                        }
                    }
                }
                else
                    PatchMod.ArmorAddons.GetOrAddAsOverride(armorAddon).AdditionalRaces.AddRange(replacementRaces);
            }

            var armors =
                from armor in LoadOrder.PriorityOrder.WinningOverrides<IArmorGetter>()
                where (!armor.MajorFlags.HasFlag(Armor.MajorFlag.NonPlayable))
                && armor.TemplateArmor.IsNull
                && armor.Armature.Any(y => armorAddonAdditions.ContainsKey(y))
                select PatchMod.Armors.GetOrAddAsOverride(armor);

            Console.WriteLine("Registering new ArmorAddons with Armors");
            foreach (var armor in armors)
                foreach (var item in armor.Armature.ToList())
                    if (armorAddonAdditions.TryGetValue(item, out var value))
                        armor.Armature.Insert(0, value);

            outfitFilesTask.Wait();
        }

        public Race CopyRace(IRaceGetter oldRace, string texturesPath, string meshesPath)
        {
            var newRace = PatchMod.Races.AddNew($"{oldRace.EditorID}_UniquePlayer");
            newRace.DeepCopyIn(oldRace, new Race.TranslationMask(defaultOn: true)
            {
                EditorID = false,
                ArmorRace = false
            });
            newRace.MorphRace.SetTo(oldRace);
            replacementPlayableRacesDict.Add(oldRace.FormKey, newRace.FormKey);

            var race = newRace;

            race.HeadData?.NotNull().ForEach(headData =>
            {
                headData.FaceDetails.NotNull().ForEach(x => TextureSets.UpdateTextureSet(x, texturesPath));

                headData.HeadParts.NotNull().ForEach(x => HeadParts.UpdateHeadPart(x.Head, texturesPath, meshesPath));

                foreach (var item in headData.TintMasks)
                {
                    var junk = false;
                    item.FileName = TexturePaths.ChangeTexturePath(item.FileName, ref junk, texturesPath);
                }

                presetCharacters.Add(headData.RacePresets);
            });
            race.RemapLinks(TextureSets.replacementTextureSets);
            race.RemapLinks(HeadParts.replacementHeadParts);

            return newRace;
        }
    }

}
