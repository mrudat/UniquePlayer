using Noggog;
using System;
using System.Collections.Generic;
using System.IO.Abstractions;

namespace UniquePlayer
{
    public class MeshPaths {
        private readonly IFileSystem _fileSystem;

        public readonly HashSet<string> inspectedMeshPaths = new();

        public readonly Dictionary<string, string> replacementMeshPathDict = new();

        public MeshPaths(IFileSystem? fileSystem = null)
        {
            _fileSystem = fileSystem ?? new FileSystem();
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

            if (!_fileSystem.File.Exists(meshesPath + testPath))
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
        public static string MangleMeshesPath(string originalPath, string injectedPath, out string testPath)
        {
            originalPath = originalPath.Replace('/', '\\');
            var indexOfMeshes = originalPath.IndexOf("Meshes\\", StringComparison.OrdinalIgnoreCase);
            if (indexOfMeshes >= 0)
                originalPath = originalPath[(indexOfMeshes + 7)..];
            testPath = $"{injectedPath}\\{originalPath}";
            return $"Meshes\\{testPath}";
        }

    } 

}
