using System;
using Xunit;
using UniquePlayer;
using System.IO.Abstractions.TestingHelpers;
using System.Collections.Generic;
using Mutagen.Bethesda;
using Mutagen.Bethesda.Skyrim;
using Moq;
using System.Linq;

namespace Tests
{
    public class TextureSets_Tests
    {
        private static readonly string TexturePath = @"Textures\";

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

            TextureSets program = new(patchMod, linkCache.Object, fileSystem: new MockFileSystem(new Dictionary<string, MockFileData>{
                { @"Textures\Player\Textures\replaced_s.dds", new("") },
                { @"Textures\Player\Textures\replaced_multilayer.dds", new("") },
                { @"Textures\Player\Textures\replaced_e.dds", new("") },
                { @"Textures\Player\Textures\replaced_height.dds", new("") },
                { @"Textures\Player\Textures\replaced_g.dds", new("") },
                { @"Textures\Player\Textures\replaced_environment.dds", new("") },
                { @"Textures\Player\Textures\replaced_n.dds", new("") },
                { @"Textures\Player\Textures\replaced_d.dds", new("") },
            }));

            var changed = program.UpdateTextureSet(textureSetFormLink, TexturePath);

            Assert.Single(patchMod.TextureSets);

            var addedTextureSet = patchMod.TextureSets.Single();

            Assert.True(changed);
            Assert.NotNull(addedTextureSet);
            Assert.Equal(@"Player\Textures\replaced_s.dds", addedTextureSet.BacklightMaskOrSpecular);
            Assert.Equal(@"Player\Textures\replaced_multilayer.dds", addedTextureSet.Multilayer);
            Assert.Equal(@"Player\Textures\replaced_e.dds", addedTextureSet.Environment);
            Assert.Equal(@"Player\Textures\replaced_height.dds", addedTextureSet.Height);
            Assert.Equal(@"Player\Textures\replaced_g.dds", addedTextureSet.GlowOrDetailMap);
            Assert.Equal(@"Player\Textures\replaced_environment.dds", addedTextureSet.EnvironmentMaskOrSubsurfaceTint);
            Assert.Equal(@"Player\Textures\replaced_n.dds", addedTextureSet.NormalOrGloss);
            Assert.Equal(@"Player\Textures\replaced_d.dds", addedTextureSet.Diffuse);

            Assert.True(program.replacementTextureSets.TryGetValue(textureSetFormKey, out var formKey));
            Assert.Equal(addedTextureSet.FormKey, formKey);
        }

        [Fact]
        public void TestUpdateTextureSetThrows()
        {
            var patchMod = new Mock<ISkyrimMod>();
            var linkCache = new Mock<ILinkCache<ISkyrimMod, ISkyrimModGetter>>();

            TextureSets program = new(patchMod.Object, linkCache.Object, fileSystem: new MockFileSystem(new Dictionary<string, MockFileData>{
                { @"Textures\Player\Textures\replaced_d.dds", new("") },
            }));

            var textureSetFormKey = MasterFormKey1;
            var textureSetFormLink = textureSetFormKey.AsLinkGetter<ITextureSetGetter>();

            Assert.Throws<RecordException>(() => program.UpdateTextureSet(textureSetFormLink, TexturePath));
        }


    }
}
