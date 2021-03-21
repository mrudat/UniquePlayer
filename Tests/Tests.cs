using System.Text;
using Mutagen.Bethesda;

namespace Tests
{
    public class Tests
    {
        public static readonly string TexturePath = @"Textures\";
        public static readonly string MeshesPath = @"Meshes\";

        public static readonly ModKey MasterModKey = ModKey.FromNameAndExtension("Master.esm");
        public static readonly ModKey PatchModKey = ModKey.FromNameAndExtension("Patch.esp");

        public static readonly FormKey MasterFormKey1 = MasterModKey.MakeFormKey(0x800);
        public static readonly FormKey MasterFormKey2 = MasterModKey.MakeFormKey(0x800);

        public static readonly FormKey PatchFormKey1 = PatchModKey.MakeFormKey(0x800);

        /*
        public void TestUpdateHeadPart() {
            Program program = new();

            var headPartFormLink = MasterFormKey1.AsLinkGetter<IHeadPartGetter>();

            var textureSetFormKey = MasterFormKey2;

            ITextureSetGetter resolveTextureSet(IFormLinkGetter<ITextureSetGetter> formLink) => new TextureSet(textureSetFormKey, SkyrimRelease.SkyrimSE)
            {
                Diffuse = "replaced_d.dds",
            };

            ITextureSet newTextureSet(string editorID) => throw new NotImplementedException();

            IMajorRecordCommonGetter race = null;

            Mutagen.Bethesda.Synthesis.IPatcherState<ISkyrimMod, ISkyrimModGetter> state = null;

            program.UpdateHeadPart(headPartFormLink, race, state, TexturePath, MeshesPath, resolveTextureSet, newTextureSet);
        }
        */
    }
}
