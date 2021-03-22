using Mutagen.Bethesda;
using Mutagen.Bethesda.Skyrim;
using Noggog;
using System;
using System.Collections.Generic;
using System.IO.Abstractions;

namespace UniquePlayer
{
    public class HeadParts
    {
        private readonly ISkyrimMod PatchMod;

        private readonly ILinkCache<ISkyrimMod, ISkyrimModGetter> LinkCache;

        private readonly TextureSets TextureSets;

        private readonly MeshPaths MeshPaths;

        public readonly Dictionary<FormKey, FormKey> replacementHeadParts = new();
        public readonly HashSet<IFormLinkGetter<IHeadPartGetter>> inspectedHeadParts = new();

        public HeadParts(
            ISkyrimMod patchMod,
            ILinkCache<ISkyrimMod, ISkyrimModGetter> linkCache,
            TextureSets? textureSets = null,
            MeshPaths? meshPaths = null,
            IFileSystem? fileSystem = null)
        {
            PatchMod = patchMod;
            LinkCache = linkCache;
            TextureSets = textureSets ?? new TextureSets(patchMod, linkCache, fileSystem: fileSystem);
            MeshPaths = meshPaths ?? new MeshPaths(fileSystem: fileSystem);
        }

        public void UpdateHeadPart(IFormLinkGetter<IHeadPartGetter> headPartItem, string texturesPath, string meshesPath)
        {
            if (inspectedHeadParts.Contains(headPartItem)) return;
            var headPartFormKey = headPartItem.FormKey;
            if (replacementHeadParts.ContainsKey(headPartFormKey)) return;
            var headPart = headPartItem.Resolve(LinkCache);
            try
            {
                var changed = false;

                if (!headPart.TextureSet.IsNull)
                    changed |= TextureSets.UpdateTextureSet(headPart.TextureSet, texturesPath);

                headPart.Parts.ForEach(x =>
                {
                    if (x.FileName != null) MeshPaths.ChangeMeshPath(x.FileName, ref changed, meshesPath);
                });

                if (headPart.Model != null)
                    MeshPaths.ChangeMeshPath(headPart.Model.File, ref changed, meshesPath);

                headPart.Model?.AlternateTextures?.ForEach(x =>
                {
                    changed |= TextureSets.UpdateTextureSet(x.NewTexture, texturesPath);
                });

                headPart.ExtraParts.ForEach(x =>
                {
                    UpdateHeadPart(x, texturesPath, meshesPath);
                });

                if (!changed)
                {
                    inspectedHeadParts.Add(headPartItem);
                    return;
                };

                var newHeadPart = PatchMod.HeadParts.AddNew($"{headPart.EditorID}_UniquePlayer");
                newHeadPart.DeepCopyIn(headPart, new HeadPart.TranslationMask(defaultOn: true)
                {
                    EditorID = false
                });
                // TODO duplicate headPart FormList and restrict to player only?

                newHeadPart.Parts.ForEach(x =>
                {
                    if (x.FileName != null) x.FileName = MeshPaths.ChangeMeshPath(x.FileName, ref changed, meshesPath);
                });

                if (newHeadPart.Model != null)
                    newHeadPart.Model.File = MeshPaths.ChangeMeshPath(newHeadPart.Model.File, ref changed, meshesPath);

                newHeadPart.RemapLinks(TextureSets.replacementTextureSets);
                replacementHeadParts.Add(headPartFormKey, newHeadPart.FormKey);
            }
            catch (Exception e)
            {
                throw RecordException.Factory(e, headPart);
            }
        }
    }

}
