using System;
using Xunit;
using UniquePlayer;
using System.IO.Abstractions.TestingHelpers;
using System.Collections.Generic;
using System.Text;
using Mutagen.Bethesda;
using Mutagen.Bethesda.Skyrim;

namespace Tests
{
    public class Tests
    {
        public static readonly string TexturePath = @"Textures\";
        public static readonly string MeshesPath = @"Meshes\";

        public static readonly TheoryData<string?, string?, bool> ChangeTexturePathData = new()
        {
            { null, null, false },
            { "texture.dds", "texture.dds", false },
            { "replaced.dds", @"Player\Textures\replaced.dds", true }
        };

        [Theory]
        [MemberData(nameof(ChangeTexturePathData))]
        public void TestChangeTexturePath(string? oldPath, string? expectedPath, bool expectedChanged)
        {
            Program program = new(new MockFileSystem(new Dictionary<string, MockFileData>{
                { @"Textures\Player\Textures\replaced.dds", new("") },
            }));

            bool changed = false;
            var newPath = program.ChangeTexturePath(oldPath, ref changed, TexturePath);
            Assert.Equal(expectedPath, newPath);
            Assert.Equal(expectedChanged, changed);

            if (oldPath == null) return;
            if (!expectedChanged) return;

            Assert.True(program.replacementTexturePathDict.TryGetValue(oldPath, out var replacementPath));
            Assert.Equal(expectedPath, replacementPath);
        }


        public class MangleMeshesPathData : TheoryData<string, string, string, string>
        {
            public MangleMeshesPathData()
            {
                foreach (var injectedPath in new[] { "Player", @"Player\Meshes", "Other" })
                    foreach (var originalPath in new[] { @"Meshes\mesh.nif", "mesh.nif" })
                        Add(originalPath, injectedPath, $@"Meshes\{injectedPath}\mesh.nif", $@"{injectedPath}\mesh.nif");
            }
        }

        [Theory]
        [ClassData(typeof(MangleMeshesPathData))]
        public void TestMangleMeshesPath(string originalPath, string injectedPath, string expectedPath, string expectedTestPath)
        {
            var newPath = Program.MangleMeshesPath(originalPath, injectedPath, out var testPath);
            Assert.Equal(expectedPath, newPath);
            Assert.Equal(expectedTestPath, testPath);
        }


        public static readonly TheoryData<string?, string?, bool> ChangeMeshPathData = new()
        {
            { "original.nif", "original.nif", false },
            { "replaced.nif", @"Meshes\Player\replaced.nif", true },
            { @"Meshes\replaced.nif", @"Meshes\Player\replaced.nif", true }
        };

        [Theory]
        [MemberData(nameof(ChangeMeshPathData))]
        public void TestChangeMeshPath(string oldPath, string expectedPath, bool expectedChanged)
        {
            Program program = new(new MockFileSystem(new Dictionary<string, MockFileData>{
                { @"Meshes\Player\replaced.nif", new("") },
            }));

            bool changed = false;
            var newPath = program.ChangeMeshPath(oldPath, ref changed, MeshesPath);
            Assert.Equal(expectedPath, newPath);
            Assert.Equal(expectedChanged, changed);

            if (!expectedChanged) return;

            Assert.True(program.replacementMeshPathDict.TryGetValue(oldPath, out var replacementPath));
            Assert.Equal(expectedPath, replacementPath);
        }

        [Fact]
        public void TestUpdateTextureSet()
        {
            Program program = new();

            var modKey = ModKey.FromNameAndExtension("Master.esp");
            var modKey2 = ModKey.FromNameAndExtension("Patch.esp");

            var textureSetFormLink = modKey.MakeFormKey(0x12345).AsLinkGetter<ITextureSetGetter>();
            var textureSet2FormLink = modKey2.MakeFormKey(0x123456);

            ITextureSetGetter resolveOrThrow(IFormLinkGetter<ITextureSetGetter> formLink, Func<string> message) => new TextureSet(textureSetFormLink.FormKey, SkyrimRelease.SkyrimSE)
            {
            };

            ITextureSet newTextureSet(string editorID) => throw new NotImplementedException("Shouldn't be called.");

            var changed = program.UpdateTextureSet(textureSetFormLink, TexturePath, resolveOrThrow, newTextureSet);

            Assert.False(changed);
            Assert.Empty(program.replacementTextureSets);
        }

        [Fact]
        public void TestUpdateTextureSet2()
        {
            Program program = new(new MockFileSystem(new Dictionary<string, MockFileData>{
                { @"Textures\Player\Textures\replaced_s.dds", new("") },
                { @"Textures\Player\Textures\replaced_multilayer.dds", new("") },
                { @"Textures\Player\Textures\replaced_e.dds", new("") },
                { @"Textures\Player\Textures\replaced_height.dds", new("") },
                { @"Textures\Player\Textures\replaced_g.dds", new("") },
                { @"Textures\Player\Textures\replaced_environment.dds", new("") },
                { @"Textures\Player\Textures\replaced_n.dds", new("") },
                { @"Textures\Player\Textures\replaced_d.dds", new("") },
            }));

            var modKey = ModKey.FromNameAndExtension("Master.esp");
            var modKey2 = ModKey.FromNameAndExtension("Patch.esp");

            var textureSetFormKey = modKey.MakeFormKey(0x12345);
            var textureSetFormLink = textureSetFormKey.AsLinkGetter<ITextureSetGetter>();
            var newTextureSetFormKey = modKey2.MakeFormKey(0x123456);

            ITextureSetGetter resolveOrThrow(IFormLinkGetter<ITextureSetGetter> formLink, Func<string> message) => new TextureSet(textureSetFormKey, SkyrimRelease.SkyrimSE)
            {
                BacklightMaskOrSpecular = "replaced_s.dds",
                Multilayer = "replaced_multilayer.dds",
                Environment = "replaced_e.dds",
                Height = "replaced_height.dds",
                GlowOrDetailMap = "replaced_g.dds",
                EnvironmentMaskOrSubsurfaceTint = "replaced_environment.dds",
                NormalOrGloss = "replaced_n.dds",
                Diffuse = "replaced_d.dds",
            };

            ITextureSetGetter addedTextureSet = null!;

            ITextureSet newTextureSet(string editorID)
            {
                var temp = new TextureSet(newTextureSetFormKey, SkyrimRelease.SkyrimSE)
                {
                };
                addedTextureSet = temp;
                return temp;
            }

            var changed = program.UpdateTextureSet(textureSetFormLink, TexturePath, resolveOrThrow, newTextureSet);

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
            Assert.Equal(newTextureSetFormKey, formKey);
        }
    }
}
