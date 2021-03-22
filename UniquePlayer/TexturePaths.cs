using System.Collections.Generic;
using System.IO.Abstractions;

namespace UniquePlayer
{
    public class TexturePaths
    {
        private readonly IFileSystem _fileSystem;

        private readonly IFile File;

        private readonly IPath Path;

        public readonly HashSet<string> inspectedTexturePaths = new();

        public readonly Dictionary<string, string> replacementTexturePathDict = new();

        public TexturePaths(IFileSystem? fileSystem = null)
        {
            _fileSystem = fileSystem ?? new FileSystem();

            File = _fileSystem.File;
            Path = _fileSystem.Path;
        }

        public string? ChangeTexturePath(string? path, ref bool changed, string texturesPath)
        {
            if (path is null) return null;
            if (inspectedTexturePaths.Contains(path)) return path;
            if (replacementTexturePathDict.TryGetValue(path, out var newPath))
            {
                changed = true;
                return newPath;
            }

            newPath = Path.Join("Player", "Textures", path);
            if (File.Exists(Path.Join(texturesPath, newPath)))
            {
                replacementTexturePathDict.Add(path, newPath);
                changed = true;
                return newPath;
            }
            inspectedTexturePaths.Add(path);
            return path;
        }
    }

}
