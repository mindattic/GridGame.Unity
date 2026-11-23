using UnityEditor;
using UnityEngine;

namespace GridGame.EditorTools
{
    /// <summary>
    /// GG-T719 (GG-T713): import settings for the baked actor thumbnails under Resources/Thumbs (tools/make_thumbs.py).
    /// <see cref="GridGameImport"/> turns mipmaps off for everything in Resources; a 256 px thumb is drawn at 40-100 px,
    /// so without mips it aliases exactly like the shrunk portraits it replaces. This runs after it
    /// (<see cref="GetPostprocessOrder"/> 1) and gives Thumbs mips, trilinear filtering, a 256 cap and CompressedHQ,
    /// with an Android ASTC 6x6 override (GG-T499). Portraits keep no mips.
    /// </summary>
    public sealed class ThumbImport : AssetPostprocessor
    {
        public const string ThumbFolder = "Assets/GridGame/Resources/Thumbs/";

        public override int GetPostprocessOrder() => 1;
        public override uint GetVersion() => 1;

        private void OnPreprocessTexture()
        {
            if (!assetPath.StartsWith(ThumbFolder)) return;
            var ti = (TextureImporter)assetImporter;
            ti.textureType = TextureImporterType.Default;
            ti.mipmapEnabled = true;
            ti.alphaIsTransparency = true;
            ti.npotScale = TextureImporterNPOTScale.None;
            ti.wrapMode = TextureWrapMode.Clamp;
            ti.filterMode = FilterMode.Trilinear;
            ti.sRGBTexture = true;
            ti.maxTextureSize = 256;
            ti.textureCompression = TextureImporterCompression.CompressedHQ;
            var a = ti.GetPlatformTextureSettings("Android");
            a.overridden = true;
            a.maxTextureSize = 256;
            a.format = TextureImporterFormat.ASTC_6x6;
            a.compressionQuality = 100;
            a.resizeAlgorithm = TextureResizeAlgorithm.Mitchell;
            ti.SetPlatformTextureSettings(a);
        }
    }
}
