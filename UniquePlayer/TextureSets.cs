using Mutagen.Bethesda;
using Mutagen.Bethesda.Skyrim;
using System;
using System.Collections.Generic;
using System.IO.Abstractions;
using Mutagen.Bethesda.Plugins;
using Mutagen.Bethesda.Plugins.Cache;
using Mutagen.Bethesda.Plugins.Exceptions;
using Mutagen.Bethesda.Plugins.Records;

namespace UniquePlayer
{
    public class TextureSets
    {
        private readonly ISkyrimMod PatchMod;

        private readonly ILinkCache<ISkyrimMod, ISkyrimModGetter> LinkCache;

        private readonly TexturePaths TexturePaths;

        public readonly HashSet<IFormLinkGetter<ITextureSetGetter>> inspectedTextureSets = new();

        public readonly Dictionary<FormKey, FormKey> replacementTextureSets = new();

        public TextureSets(ISkyrimMod patchMod, ILinkCache<ISkyrimMod, ISkyrimModGetter> linkCache, TexturePaths? texturePaths = null, IFileSystem? fileSystem = null)
        {
            PatchMod = patchMod;
            LinkCache = linkCache;
            TexturePaths = texturePaths ?? new TexturePaths(fileSystem: fileSystem);
        }

        public bool UpdateTextureSet(IFormLinkGetter<ITextureSetGetter> textureSetFormLink, string texturesPath)
        {
            if (textureSetFormLink.IsNull) return false;
            if (inspectedTextureSets.Contains(textureSetFormLink)) return false;
            var textureSetFormKey = textureSetFormLink.FormKey;
            if (replacementTextureSets.ContainsKey(textureSetFormKey)) return true;
            var txst = textureSetFormLink.Resolve(LinkCache);
            try
            {
                var changed = false;
                TexturePaths.ChangeTexturePath(txst.Diffuse, ref changed, texturesPath);
                TexturePaths.ChangeTexturePath(txst.NormalOrGloss, ref changed, texturesPath);
                TexturePaths.ChangeTexturePath(txst.EnvironmentMaskOrSubsurfaceTint, ref changed, texturesPath);
                TexturePaths.ChangeTexturePath(txst.GlowOrDetailMap, ref changed, texturesPath);
                TexturePaths.ChangeTexturePath(txst.Height, ref changed, texturesPath);
                TexturePaths.ChangeTexturePath(txst.Environment, ref changed, texturesPath);
                TexturePaths.ChangeTexturePath(txst.Multilayer, ref changed, texturesPath);
                TexturePaths.ChangeTexturePath(txst.BacklightMaskOrSpecular, ref changed, texturesPath);

                if (!changed)
                {
                    inspectedTextureSets.Add(textureSetFormLink);
                    return false;
                }

                var newTxst = PatchMod.TextureSets.AddNew($"{txst.EditorID}_UniquePlayer");
                newTxst.DeepCopyIn(txst, new TextureSet.TranslationMask(defaultOn: true)
                {
                    EditorID = false
                });
                replacementTextureSets.Add(textureSetFormKey, newTxst.FormKey);

                newTxst.Diffuse = TexturePaths.ChangeTexturePath(txst.Diffuse, ref changed, texturesPath);
                newTxst.NormalOrGloss = TexturePaths.ChangeTexturePath(txst.NormalOrGloss, ref changed, texturesPath);
                newTxst.EnvironmentMaskOrSubsurfaceTint = TexturePaths.ChangeTexturePath(txst.EnvironmentMaskOrSubsurfaceTint, ref changed, texturesPath);
                newTxst.GlowOrDetailMap = TexturePaths.ChangeTexturePath(txst.GlowOrDetailMap, ref changed, texturesPath);
                newTxst.Height = TexturePaths.ChangeTexturePath(txst.Height, ref changed, texturesPath);
                newTxst.Environment = TexturePaths.ChangeTexturePath(txst.Environment, ref changed, texturesPath);
                newTxst.Multilayer = TexturePaths.ChangeTexturePath(txst.Multilayer, ref changed, texturesPath);
                newTxst.BacklightMaskOrSpecular = TexturePaths.ChangeTexturePath(txst.BacklightMaskOrSpecular, ref changed, texturesPath);
                return true;
            }
            catch (Exception e)
            {
                throw RecordException.Factory("UpdatTextureSet", txst, e);
            }
        }

    }

}
