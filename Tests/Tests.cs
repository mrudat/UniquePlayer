using System;
using Xunit;
using UniquePlayer;
using System.IO.Abstractions.TestingHelpers;
using System.Collections.Generic;

namespace Tests
{
    public class Tests
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
            Program program = new(new MockFileSystem(new Dictionary<string, MockFileData>{
                { @"Textures\Player\Textures\replaced.dds", new("") },
            }));

            bool changed = false;
            var newPath = program.ChangeTexturePath(oldPath, ref changed, TexturePath);
            Assert.Equal(expectedPath, newPath);
            Assert.Equal(expectedChanged, changed);
        }

    }
}
