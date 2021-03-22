using System.Collections.Generic;
using System.IO.Abstractions.TestingHelpers;
using UniquePlayer;
using Xunit;

namespace Tests
{
    public class TexturePaths_Tests
    {
        public static readonly string TexturePath = @"Textures\";

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
            TexturePaths program = new(new MockFileSystem(new Dictionary<string, MockFileData>{
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
    }
}
