using Mutagen.Bethesda.Synthesis;
using Noggog;
using System.IO;
using System.IO.Abstractions;

namespace UniquePlayer
{
    public class RunnabilityCheck
    {
        private readonly IRunnabilityState State;

        private readonly IFileSystem _fileSystem;

        private readonly Settings Settings;

        public RunnabilityCheck(IRunnabilityState state, Settings settings, IFileSystem? fileSystem = null)
        {
            State = state;
            Settings = settings;

            _fileSystem = fileSystem ?? new FileSystem();
        }

        public void Check()
        {
            var dataPath = State.Settings.DataFolderPath;
            if (Settings.CustomBodyslideInstallPath)
            {
                dataPath = Settings.BodySlideInstallPath;
            }
            Program.BodySlidePaths(dataPath, out string outfitsPath, out string groupsPath);

            if (!(_fileSystem.Directory.Exists(outfitsPath) && _fileSystem.Directory.Exists(groupsPath)))
                throw new FileNotFoundException("Bodyslide installation not in default location, cannot proceed.");
        }
    }

}
