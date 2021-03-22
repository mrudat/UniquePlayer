using System.Collections.Generic;
using System.IO;
using System.IO.Abstractions.TestingHelpers;
using UniquePlayer;
using Xunit;

namespace Tests
{
    public class MeshPaths_Tests
    {
        public static readonly string MeshesPath = "Meshes";

        public class MangleMeshesPathData : TheoryData<string, string, string, string>
        {
            public MangleMeshesPathData()
            {
                foreach (var injectedPath in new[] { "Player", Path.Join("Player", MeshesPath), "Other" })
                    foreach (var originalPath in new[] { Path.Join(MeshesPath, "mesh.nif"), "mesh.nif" })
                        Add(originalPath, injectedPath, Path.Join(MeshesPath, injectedPath, "mesh.nif"), Path.Join(injectedPath, "mesh.nif"));
            }
        }

        [Theory]
        [ClassData(typeof(MangleMeshesPathData))]
        public void TestMangleMeshesPath(string originalPath, string injectedPath, string expectedPath, string expectedTestPath)
        {
            var meshPaths = new MeshPaths();

            var newPath = meshPaths.MangleMeshesPath(originalPath, injectedPath, out var testPath);
            Assert.Equal(expectedPath, newPath);
            Assert.Equal(expectedTestPath, testPath);
        }


        public static readonly TheoryData<string?, string?, bool> ChangeMeshPathData = new()
        {
            { "original.nif", "original.nif", false },
            { "replaced.nif", Path.Join("Meshes", "Player", "replaced.nif"), true },
            { Path.Join("Meshes", "replaced.nif"), Path.Join("Meshes", "Player", "replaced.nif"), true }
        };

        [Theory]
        [MemberData(nameof(ChangeMeshPathData))]
        public void TestChangeMeshPath(string oldPath, string expectedPath, bool expectedChanged)
        {
            MeshPaths program = new(new MockFileSystem(new Dictionary<string, MockFileData>{
                { Path.Join("Meshes", "Player", "replaced.nif"), new("") },
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
