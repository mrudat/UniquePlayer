using FluentAssertions;
using System.Collections.Generic;
using System.IO;
using System.IO.Abstractions.TestingHelpers;
using System.Xml.Linq;
using UniquePlayer;
using Xunit;

namespace Tests
{
    public class CopyAndModifyOutfitFiles_Tests
    {
        public static readonly string BodySlidePath = Path.Combine(Directory.GetCurrentDirectory(), "Tools", "BodySlide");
        public static readonly string DataFolderPath = Path.Combine(Directory.GetCurrentDirectory(), "Data");

        [Fact]
        public void TestExplicitInstallPath()
        {
            var bodySlidePath = BodySlidePath;

            var sliderSetPath = Path.Combine(bodySlidePath, "SliderSets");

            var sliderGroupPath = Path.Combine(bodySlidePath, "SliderGroups");

            var fileSystem = new MockFileSystem(new Dictionary<string, MockFileData>() {
                { sliderSetPath, new MockDirectoryData() },
                { sliderGroupPath, new MockDirectoryData() },
            });

            new CopyAndModifyOutfitFiles(BodySlidePath, DataFolderPath, fileSystem: fileSystem).Run();

            Assert.True(fileSystem.FileExists(Path.Combine(sliderSetPath, "UniquePlayer.osp")));
            Assert.True(fileSystem.FileExists(Path.Combine(sliderGroupPath, "UniquePlayer.xml")));
        }

        [Fact]
        public void TestDefaultInstallPath()
        {
            var bodySlidePath = Path.Combine(DataFolderPath, "CalienteTools", "BodySlide");

            var sliderSetPath = Path.Combine(bodySlidePath, "SliderSets");

            var sliderGroupPath = Path.Combine(bodySlidePath, "SliderGroups");

            var fileSystem = new MockFileSystem(new Dictionary<string, MockFileData>() {
                { sliderSetPath, new MockDirectoryData() },
                { sliderGroupPath, new MockDirectoryData() },
            });

            new CopyAndModifyOutfitFiles(null, DataFolderPath, fileSystem: fileSystem).Run();

            Assert.True(fileSystem.FileExists(Path.Combine(sliderSetPath, "UniquePlayer.osp")));
            Assert.True(fileSystem.FileExists(Path.Combine(sliderGroupPath, "UniquePlayer.xml")));
        }

        [Fact]
        public void TestInvalidOutfit()
        {
            var bodySlidePath = BodySlidePath;

            var sliderSetPath = Path.Combine(bodySlidePath, "SliderSets");

            var sliderGroupPath = Path.Combine(bodySlidePath, "SliderGroups");

            var fileSystem = new MockFileSystem(new Dictionary<string, MockFileData>() {
                { sliderSetPath, new MockDirectoryData() },
                { Path.Combine(sliderSetPath, "TestOutfit.osp"), new MockFileData("invalid outfit file gets skipped") },
                { sliderGroupPath, new MockDirectoryData() },
            });

            new CopyAndModifyOutfitFiles(BodySlidePath, DataFolderPath, fileSystem: fileSystem).Run();

            Assert.True(fileSystem.FileExists(Path.Combine(sliderSetPath, "UniquePlayer.osp")));
            Assert.True(fileSystem.FileExists(Path.Combine(sliderGroupPath, "UniquePlayer.xml")));
        }

        [Fact]
        public void TestInvalidGroup()
        {
            var bodySlidePath = BodySlidePath;

            var sliderSetPath = Path.Combine(bodySlidePath, "SliderSets");

            var sliderGroupPath = Path.Combine(bodySlidePath, "SliderGroups");

            var fileSystem = new MockFileSystem(new Dictionary<string, MockFileData>() {
                { sliderSetPath, new MockDirectoryData() },
                { Path.Combine(sliderGroupPath, "TestOutfit.xml"), new MockFileData("invalid group file gets skipped") },
                { sliderGroupPath, new MockDirectoryData() },
            });

            new CopyAndModifyOutfitFiles(BodySlidePath, DataFolderPath, fileSystem: fileSystem).Run();

            Assert.True(fileSystem.FileExists(Path.Combine(sliderSetPath, "UniquePlayer.osp")));
            Assert.True(fileSystem.FileExists(Path.Combine(sliderGroupPath, "UniquePlayer.xml")));
        }

        [Fact]
        public void TestOutfitModification()
        {
            var bodySlidePath = BodySlidePath;

            var sliderSetPath = Path.Combine(bodySlidePath, "SliderSets");

            var sliderGroupPath = Path.Combine(bodySlidePath, "SliderGroups");

            var originalOutfit = new XDocument(
                new XDeclaration("1.0", "utf-8", "yes"),
                new XElement("SliderSetInfo",
                    new XAttribute("version", "1"),
                    new XElement("SliderSet",
                        new XAttribute("name", "originalOutfit"),
                        new XElement("OutputPath", Path.Combine("meshes", "originalOutfit"))
                        ))
            ).ToString();

            var fileSystem = new MockFileSystem(new Dictionary<string, MockFileData>() {
                { Path.Combine(sliderSetPath, "TestOutfit.osp"), new MockFileData(originalOutfit) },
                { sliderGroupPath, new MockDirectoryData() },
            });

            new CopyAndModifyOutfitFiles(BodySlidePath, DataFolderPath, fileSystem: fileSystem).Run();

            var outputOutfitsPath = Path.Combine(sliderSetPath, "UniquePlayer.osp");

            Assert.True(fileSystem.FileExists(outputOutfitsPath));
            Assert.True(fileSystem.FileExists(Path.Combine(sliderGroupPath, "UniquePlayer.xml")));

            using var file = fileSystem.File.OpenRead(outputOutfitsPath);

            var outputOutfits = XDocument.Load(file, LoadOptions.PreserveWhitespace);

            var declaration = outputOutfits.Declaration
                .Should().NotBeNull()
                .And.BeOfType<XDeclaration>()
                .Subject;

            declaration.Version.Should().Be("1.0");
            declaration.Encoding.Should().Be("utf-8");
            declaration.Standalone.Should().Be("yes");

            outputOutfits
                .Should().HaveRoot("SliderSetInfo")
                .Which.Should().HaveAttribute("version", "1")
                .And.HaveElement("SliderSet")
                .Which.Should().HaveAttribute("name", "originalOutfit (unknown) (Unique Player)")
                .And.HaveElement("OutputPath")
                .Which.Should().HaveValue(Path.Combine("Meshes", "Player", "originalOutfit"));
        }

    }
}
