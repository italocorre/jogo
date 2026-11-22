using UnityEditor;
using UnityEngine;

namespace MarAberto.EditorTools
{
    /// <summary>Configura a importação das texturas da interface, das fontes e dos sons.</summary>
    public class AssetImport : AssetPostprocessor
    {
        void OnPreprocessTexture()
        {
            if (assetPath.Contains("Resources/Textures/"))
            {
                // Pinceladas e papel: dados (não cor), repetem sem emenda.
                var tt = (TextureImporter)assetImporter;
                tt.textureType = TextureImporterType.Default;
                tt.sRGBTexture = false;
                tt.mipmapEnabled = true;
                tt.wrapMode = TextureWrapMode.Repeat;
                tt.filterMode = FilterMode.Trilinear;
                tt.anisoLevel = 4;
                tt.textureCompression = TextureImporterCompression.Uncompressed;
                return;
            }
            if (!assetPath.Contains("Resources/UI/")) return;
            var ti = (TextureImporter)assetImporter;
            bool cursor = assetPath.EndsWith("cursor.png");
            ti.textureType = cursor ? TextureImporterType.Cursor : TextureImporterType.Default;
            ti.mipmapEnabled = false;
            ti.alphaIsTransparency = true;
            ti.wrapMode = TextureWrapMode.Clamp;
            ti.filterMode = FilterMode.Bilinear;
            ti.textureCompression = TextureImporterCompression.Uncompressed;
            ti.npotScale = TextureImporterNPOTScale.None;
            ti.isReadable = cursor;
            ti.sRGBTexture = true;
        }

        void OnPreprocessAudio()
        {
            if (!assetPath.Contains("Resources/Audio/")) return;
            var ai = (AudioImporter)assetImporter;
            var s = ai.defaultSampleSettings;
            string file = System.IO.Path.GetFileName(assetPath);
            bool longClip = file.StartsWith("musica") || file.StartsWith("amb_");
            s.loadType = longClip ? AudioClipLoadType.Streaming : AudioClipLoadType.DecompressOnLoad;
            s.compressionFormat = AudioCompressionFormat.Vorbis;
            s.quality = 0.7f;
            ai.defaultSampleSettings = s;
            ai.forceToMono = false;
        }
    }
}
