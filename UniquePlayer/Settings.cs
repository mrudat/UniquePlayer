namespace UniquePlayer
{
    public record Settings
    {
        public bool CustomBodyslideInstallPath = false;

        public string BodySlideInstallPath = "";

        public string? GetBodySlideInstallPath()
        {
            if (CustomBodyslideInstallPath)
                return BodySlideInstallPath;
            return null;
        }
    }
}
