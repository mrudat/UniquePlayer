using System;
using Xunit;
using UniquePlayer;
using System.IO.Abstractions.TestingHelpers;
using System.Collections.Generic;
using System.Text;

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


        public class MangleMeshesPathData : TheoryData<string, string, string, string> {
            public MangleMeshesPathData() {
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

    }
}
