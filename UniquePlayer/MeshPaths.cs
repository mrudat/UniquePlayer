using System;
using System.Collections.Generic;
using System.IO.Abstractions;

namespace UniquePlayer
{
    public class MeshPaths
    {
        private readonly IFileSystem _fileSystem;
        private readonly IFile File;
        private readonly IDirectoryInfoFactory DirectoryInfo;
        private readonly IPath Path;
        private readonly IDirectory Directory;
        public readonly HashSet<string> inspectedMeshPaths = new();

        public readonly Dictionary<string, string> replacementMeshPathDict = new();

        public MeshPaths(IFileSystem? fileSystem = null)
        {
            _fileSystem = fileSystem ?? new FileSystem();

            File = _fileSystem.File;
            DirectoryInfo = _fileSystem.DirectoryInfo;
            Path = _fileSystem.Path;
            Directory = _fileSystem.Directory;
        }

        public string ChangeMeshPath(string path, ref bool changed, string meshesPath)
        {
            if (inspectedMeshPaths.Contains(path)) return path;
            if (replacementMeshPathDict.TryGetValue(path, out var newPath))
            {
                changed = true;
                return newPath;
            }

            newPath = MangleMeshesPath(path, "Player", out var testPath);

            if (!File.Exists(Path.Join(meshesPath, testPath)))
            {
                inspectedMeshPaths.Add(path);
                return path;
            }

            replacementMeshPathDict.Add(path, newPath);
            changed = true;

            return newPath;
        }

        /// <summary>
        /// Edits a path to a mesh file using Skyrim's mesh path rules.
        /// </summary>
        /// <returns>$"Meshes/{injectedPath}/..."</returns>
        /// <param name="testPath">$"{injectedPath}/..."</param>
        public string MangleMeshesPath(string originalPath, string injectedPath, out string testPath)
        {
            testPath = Path.Join(injectedPath, RemoveMeshesPath(originalPath));
            return Path.Join("Meshes", testPath);
        }

        public string RemoveMeshesPath(string originalPath)
        {
            // FIXME a version of this that ignores the filesystem.
            var directory = Path.GetDirectoryName(originalPath);
            if (directory.Length == 0) return originalPath;

            var fileName = Path.GetFileName(originalPath);

            if (directory.Equals("Meshes", StringComparison.OrdinalIgnoreCase)) return fileName;

            var originalPathComponents = DirectoryInfo.FromDirectoryName(directory);

            int meshesDir = 0;

            Stack<string> components = new();

            var rootName = originalPathComponents.Root.Name;
            var cwd = Directory.GetCurrentDirectory();

            for (var dir = originalPathComponents; dir != null; dir = dir.Parent)
            {
                if (dir.Name == rootName) break; // eh. this is fine.
                meshesDir++;
                if (dir.FullName == cwd) break; // oh god, why?
                if (dir.Name.Equals("Meshes", StringComparison.OrdinalIgnoreCase))
                    meshesDir = 1;
                components.Push(dir.Name);
            }

            if (meshesDir != components.Count)
                for (int i = meshesDir; i > 0; i--)
                    components.Pop();

            return Path.Join(Path.Combine(components.ToArray()), fileName);
        }

    }

}
