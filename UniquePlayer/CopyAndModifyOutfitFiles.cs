using Noggog;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO.Abstractions;
using System.Linq;
using System.Xml.Linq;

namespace UniquePlayer
{
    public class CopyAndModifyOutfitFiles
    {
        private readonly IFileSystem _fileSystem;
        private readonly MeshPaths MeshPaths;
        private readonly IFile File;
        private readonly IDirectory Directory;
        private readonly System.IO.Abstractions.IPath Path;
        private readonly string BodySlideInstallPath;

        public CopyAndModifyOutfitFiles(string? bodySlidePath, string dataFolderPath, MeshPaths? meshPaths = null, IFileSystem? fileSystem = null)
        {
            _fileSystem = fileSystem ?? new FileSystem();

            MeshPaths = meshPaths ?? new MeshPaths(_fileSystem);

            File = _fileSystem.File;
            Directory = _fileSystem.Directory;
            Path = _fileSystem.Path;

            BodySlideInstallPath = bodySlidePath ?? Path.Join(dataFolderPath, "CalienteTools", "BodySlide");
        }

        private static readonly Dictionary<string, string> BodyReferenceToBodyName = new()
        {
            { "CBBE Hands", "CBBE" },
            { "CBBE Body", "CBBE" },
            { "CBBE Feet", "CBBE" },
            { "TBD - Reference - Default Body", "TBD" },
        };

        XDocument? TryLoad(string path)
        {
            try
            {
                using var file = File.OpenRead(path);
                return XDocument.Load(file, LoadOptions.PreserveWhitespace);
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                return null;
            }
        }

        public void Run()
        {
            var outfitsPath = Path.Join(BodySlideInstallPath, "SliderSets");
            var groupsPath = Path.Join(BodySlideInstallPath, "SliderGroups");

            if (!(Directory.Exists(outfitsPath) && Directory.Exists(groupsPath)))
            {
                Console.WriteLine("Bodyslide installation not found, cannot create modified outfits.");
                return;
            }

            var outfitOutputFileName = "UniquePlayer.osp";
            var groupOutputFileName = "UniquePlayer.xml";

            var oldToNewOutfitNames = new Dictionary<string, string>();

            var outfitsData = (
                from filePath in Directory.GetFiles(outfitsPath).AsParallel()
                where filePath.EndsWith(".osp")
                   && !filePath.EndsWith(outfitOutputFileName)
                   && File.Exists(filePath)
                let doc = TryLoad(filePath)
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
                from filePath in Directory.GetFiles(groupsPath) //.AsParallel()
                where filePath.EndsWith(".xml")
                   && !filePath.EndsWith(groupOutputFileName)
                   && File.Exists(filePath)
                let doc = TryLoad(filePath)
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

            void SaveDoc(string path, string file, XDocument doc) => doc.Save(File.OpenWrite(Path.Join(path, file)));

            // TODO if no data found, don't emit anything?
            SaveDoc(outfitsPath, outfitOutputFileName, outfitsDoc);

            SaveDoc(groupsPath, groupOutputFileName, sliderGroupDoc);
        }


    }
}
