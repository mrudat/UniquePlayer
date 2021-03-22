using Moq;
using Mutagen.Bethesda;
using Mutagen.Bethesda.Skyrim;
using System.Collections.Generic;
using System.IO;
using System.IO.Abstractions.TestingHelpers;
using System.Linq;
using UniquePlayer;
using Xunit;

namespace Tests
{
    public class TextureSets_Tests
    {
        private static readonly string TexturePath = "Textures";

        private static readonly ModKey MasterModKey = ModKey.FromNameAndExtension("Master.esm");

        private static readonly ModKey PatchModKey = ModKey.FromNameAndExtension("Patch.esp");

        private static readonly FormKey MasterFormKey1 = MasterModKey.MakeFormKey(0x800);

        private static readonly FormKey PatchFormKey1 = PatchModKey.MakeFormKey(0x800);

        [Fact]
        public void TestUpdateTextureSet()
        {
            var patchMod = new Mock<ISkyrimMod>();

            var linkCache = new Mock<ILinkCache<ISkyrimMod, ISkyrimModGetter>>();

            var modKey = MasterModKey;
            var modKey2 = PatchModKey;

            var textureSetFormKey = MasterFormKey1;
            var textureSetFormLink = textureSetFormKey.AsLinkGetter<ITextureSetGetter>();
            var textureSet2FormLink = PatchFormKey1;


            ITextureSetGetter? oldTextureSet = new TextureSet(textureSetFormKey, SkyrimRelease.SkyrimSE)
            {
                EditorID = "oldTextureSet",
            };

            linkCache.Setup(x => x.TryResolve<ITextureSetGetter>(textureSetFormKey, out oldTextureSet)).Returns(true);

            TextureSets program = new(patchMod.Object, linkCache.Object);

            var changed = program.UpdateTextureSet(textureSetFormLink, TexturePath);

            Assert.False(changed);
            Assert.Empty(program.replacementTextureSets);
        }

        [Fact]
        public void TestUpdateTextureSet2()
        {
            var linkCache = new Mock<ILinkCache<ISkyrimMod, ISkyrimModGetter>>();

            var textureSetFormKey = MasterFormKey1;
            var textureSetFormLink = textureSetFormKey.AsLinkGetter<ITextureSetGetter>();

            ITextureSetGetter? oldTextureSet = new TextureSet(textureSetFormKey, SkyrimRelease.SkyrimSE)
            {
                EditorID = "oldTextureSet",
                BacklightMaskOrSpecular = "replaced_s.dds",
                Multilayer = "replaced_multilayer.dds",
                Environment = "replaced_e.dds",
                Height = "replaced_height.dds",
                GlowOrDetailMap = "replaced_g.dds",
                EnvironmentMaskOrSubsurfaceTint = "replaced_environment.dds",
                NormalOrGloss = "replaced_n.dds",
                Diffuse = "replaced_d.dds",
            };

            linkCache.Setup(x => x.TryResolve<ITextureSetGetter>(textureSetFormKey, out oldTextureSet)).Returns(true);

            var patchMod = new SkyrimMod(PatchModKey, SkyrimRelease.SkyrimSE);

            var replacedTexturesPath = Path.Join("Textures","Player","Textures");

            TextureSets program = new(patchMod, linkCache.Object, fileSystem: new MockFileSystem(new Dictionary<string, MockFileData>{
                { Path.Join(replacedTexturesPath, "replaced_s.dds"), new("") },
                { Path.Join(replacedTexturesPath, "replaced_multilayer.dds"), new("") },
                { Path.Join(replacedTexturesPath, "replaced_e.dds"), new("") },
                { Path.Join(replacedTexturesPath, "replaced_height.dds"), new("") },
                { Path.Join(replacedTexturesPath, "replaced_g.dds"), new("") },
                { Path.Join(replacedTexturesPath, "replaced_environment.dds"), new("") },
                { Path.Join(replacedTexturesPath, "replaced_n.dds"), new("") },
                { Path.Join(replacedTexturesPath, "replaced_d.dds"), new("") },
            }));

            var changed = program.UpdateTextureSet(textureSetFormLink, TexturePath);

            Assert.Single(patchMod.TextureSets);

            var addedTextureSet = patchMod.TextureSets.Single();

            var expectedTexturesPath = Path.Join("Player", "Textures");

            Assert.True(changed);
            Assert.NotNull(addedTextureSet);
            Assert.Equal(Path.Join(expectedTexturesPath, "replaced_s.dds"), addedTextureSet.BacklightMaskOrSpecular);
            Assert.Equal(Path.Join(expectedTexturesPath, "replaced_multilayer.dds"), addedTextureSet.Multilayer);
            Assert.Equal(Path.Join(expectedTexturesPath, "replaced_e.dds"), addedTextureSet.Environment);
            Assert.Equal(Path.Join(expectedTexturesPath, "replaced_height.dds"), addedTextureSet.Height);
            Assert.Equal(Path.Join(expectedTexturesPath, "replaced_g.dds"), addedTextureSet.GlowOrDetailMap);
            Assert.Equal(Path.Join(expectedTexturesPath, "replaced_environment.dds"), addedTextureSet.EnvironmentMaskOrSubsurfaceTint);
            Assert.Equal(Path.Join(expectedTexturesPath, "replaced_n.dds"), addedTextureSet.NormalOrGloss);
            Assert.Equal(Path.Join(expectedTexturesPath, "replaced_d.dds"), addedTextureSet.Diffuse);

            Assert.True(program.replacementTextureSets.TryGetValue(textureSetFormKey, out var formKey));
            Assert.Equal(addedTextureSet.FormKey, formKey);
        }

        [Fact]
        public void TestUpdateTextureSetThrows()
        {
            var patchMod = new Mock<ISkyrimMod>();
            var linkCache = new Mock<ILinkCache<ISkyrimMod, ISkyrimModGetter>>();

            TextureSets program = new(patchMod.Object, linkCache.Object, fileSystem: new MockFileSystem(new Dictionary<string, MockFileData>{
                { Path.Join("Textures", "Player", "Textures", "replaced_d.dds"), new("") },
            }));

            var textureSetFormKey = MasterFormKey1;
            var textureSetFormLink = textureSetFormKey.AsLinkGetter<ITextureSetGetter>();

            Assert.Throws<RecordException>(() => program.UpdateTextureSet(textureSetFormLink, TexturePath));
        }


    }
}
