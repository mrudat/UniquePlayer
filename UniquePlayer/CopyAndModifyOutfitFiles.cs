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

            var originalOutfits = new HashSet<string>();
            var newOutfits = new HashSet<string>();

            foreach (var (oldOutfitName, outfitData) in outfitsData)
            {
                var outfitPathElement = outfitData.Element("OutputPath");
                if (outfitPathElement is null) continue;
                outfitPathElement.Value = MeshPaths.MangleMeshesPath(outfitPathElement.Value, "Player", out _);

                originalOutfits.Add(oldOutfitName);

                var newOutfitName = oldOutfitName + " (Unique Player)";

                newOutfits.Add(newOutfitName);

                outfitData.Attribute("name")!.Value = newOutfitName;

                sliderSetInfo.Add(outfitData);

                oldToNewOutfitNames[oldOutfitName] = newOutfitName;
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
                join rec in oldToNewOutfitNames // only includes existing outfit names
                on outfitName equals rec.Key
                let newOutfitName = rec.Value
                group newOutfitName by outfitGroupName;

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
                var outfitGroupName = item.Key;

                outfitGroupName += " (Unique Player)";

                sliderGroups.Add(MakeSliderGroup(item, outfitGroupName));
            }

            sliderGroups.Add(MakeSliderGroup(originalOutfits, "Original"));
            sliderGroups.Add(MakeSliderGroup(newOutfits, "Unique Player"));

            void SaveDoc(string path, string file, XDocument doc) => doc.Save(File.OpenWrite(Path.Join(path, file)));

            // TODO if no data found, don't emit anything?
            SaveDoc(outfitsPath, outfitOutputFileName, outfitsDoc);

            SaveDoc(groupsPath, groupOutputFileName, sliderGroupDoc);
        }


    }
}
