using Moq;
using Mutagen.Bethesda;
using Mutagen.Bethesda.Skyrim;
using System.Collections.Generic;
using System.IO;
using System.IO.Abstractions.TestingHelpers;
using System.Linq;
using Mutagen.Bethesda.Plugins;
using Mutagen.Bethesda.Plugins.Cache;
using Mutagen.Bethesda.Plugins.Exceptions;
using UniquePlayer;
using Xunit;

namespace Tests
{
    public class UpdateHeadParts_Tests
    {
        public static readonly string TexturePath = "Textures";
        public static readonly string MeshesPath = "Meshes";

        public static readonly ModKey MasterModKey = ModKey.FromNameAndExtension("Master.esm");
        public static readonly ModKey PatchModKey = ModKey.FromNameAndExtension("Patch.esp");

        public static readonly FormKey MasterFormKey1 = MasterModKey.MakeFormKey(0x800);
        public static readonly FormKey MasterFormKey2 = MasterModKey.MakeFormKey(0x800);

        public static readonly FormKey PatchFormKey1 = PatchModKey.MakeFormKey(0x800);

        [Fact]
        public static void TestUpdateHeadPartThrows()
        {
            var patchMod = new Mock<ISkyrimMod>();

            var linkCache = new Mock<ILinkCache<ISkyrimMod, ISkyrimModGetter>>();

            HeadParts program = new(patchMod.Object, linkCache.Object);

            var headPartFormLink = MasterFormKey1.AsLinkGetter<IHeadPartGetter>();

            Assert.Throws<RecordException>(() => program.UpdateHeadPart(headPartFormLink, TexturePath, MeshesPath));
        }

        [Fact]
        public static void TestUpdateHeadPart()
        {
            var patchMod = new SkyrimMod(PatchModKey, SkyrimRelease.SkyrimSE);

            var masterMod = new SkyrimMod(MasterModKey, SkyrimRelease.SkyrimSE);

            var oldHeadPart = masterMod.HeadParts.AddNew("oldHeadPart");

            var headPartFormLink = oldHeadPart.AsLink();

            var linkCache = masterMod.ToImmutableLinkCache();

            HeadParts program = new(patchMod, linkCache);

            program.UpdateHeadPart(headPartFormLink, TexturePath, MeshesPath);

            Assert.Empty(patchMod.TextureSets);
        }

        [Fact]
        public static void TestUpdateHeadPart2()
        {
            var patchMod = new SkyrimMod(PatchModKey, SkyrimRelease.SkyrimSE);

            var masterMod = new SkyrimMod(MasterModKey, SkyrimRelease.SkyrimSE);

            var oldHeadPart = masterMod.HeadParts.AddNew("oldHeadPart");

            (oldHeadPart.Model ??= new()).File = Path.Join(MeshesPath, "mesh.nif");

            var headPartFormLink = oldHeadPart.AsLink();

            var linkCache = masterMod.ToImmutableLinkCache();

            var newMeshPath = Path.Join(MeshesPath, "Player", "mesh.nif");

            var fileSystem = new MockFileSystem(new Dictionary<string, MockFileData>() {
                { newMeshPath, new MockFileData("") }
            });

            HeadParts program = new(patchMod, linkCache, fileSystem: fileSystem);

            program.UpdateHeadPart(headPartFormLink, TexturePath, MeshesPath);

            Assert.Single(patchMod.HeadParts);

            var newHeadPart = patchMod.HeadParts.Single();

            Assert.Equal(newMeshPath, newHeadPart.Model?.File);
        }
    }
}
