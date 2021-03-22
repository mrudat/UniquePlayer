using Mutagen.Bethesda.Synthesis;
using Synthesis.Bethesda;
using System.Collections.Generic;
using System.IO;
using System.IO.Abstractions.TestingHelpers;
using UniquePlayer;
using Xunit;

namespace Tests
{
    public class RunnabilityCheck_Tests
    {
        [Fact]
        public void TestMissing()
        {
            CheckRunnability checkRunnability = new();
            var state = new RunnabilityState(checkRunnability, null!);
            Settings settings = new();

            var fileSystem = new MockFileSystem();

            Assert.Throws<FileNotFoundException>(() => new RunnabilityCheck(state, settings, fileSystem).Check());
        }

        [Fact]
        public void TestMissingCustom()
        {
            CheckRunnability checkRunnability = new();
            var state = new RunnabilityState(checkRunnability, null!);
            Settings settings = new()
            {
                CustomBodyslideInstallPath = true,
                BodySlideInstallPath = @"c:\does\not\exist\",
            };

            var fileSystem = new MockFileSystem();

            Assert.Throws<FileNotFoundException>(() => new RunnabilityCheck(state, settings, fileSystem).Check());
        }

        [Fact]
        public void TestFound()
        {
            CheckRunnability checkRunnability = new();
            var state = new RunnabilityState(checkRunnability, null!);
            Settings settings = new();

            var dataFolderPath = @"Program Files (x86)\Steam\steamapps\common\Skyrim\Data\";

            checkRunnability.DataFolderPath = dataFolderPath;

            var bodySlideInstallPath = dataFolderPath + @"CalienteTools\BodySlide\";

            var fileSystem = new MockFileSystem(new Dictionary<string, MockFileData>() {
                { bodySlideInstallPath + @"SliderSets\", new MockDirectoryData() },
                { bodySlideInstallPath + @"SliderGroups\", new MockDirectoryData() }
            });

            new RunnabilityCheck(state, settings, fileSystem).Check();
        }

        [Fact]
        public void TestFoundCustom()
        {
            CheckRunnability checkRunnability = new();
            var state = new RunnabilityState(checkRunnability, null!);

            var bodySlideInstallPath = @"where\bodyslide\and\outfitstudio\are\installed\";

            Settings settings = new()
            {
                CustomBodyslideInstallPath = true,
                BodySlideInstallPath = bodySlideInstallPath,
            };

            // FIXME should not add this to the configured path, only the default!
            bodySlideInstallPath += @"CalienteTools\BodySlide\";

            var fileSystem = new MockFileSystem(new Dictionary<string, MockFileData>() {
                { bodySlideInstallPath + @"SliderSets\", new MockDirectoryData() },
                { bodySlideInstallPath + @"SliderGroups\", new MockDirectoryData() }
            });

            new RunnabilityCheck(state, settings, fileSystem).Check();
        }
    }
}
