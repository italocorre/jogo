using UnityEditor;
using UnityEngine;

namespace MarAberto.EditorTools
{
    /// <summary>Configura a importação das texturas da interface, das fontes e dos sons.</summary>
    public class AssetImport : AssetPostprocessor
    {
        void OnPreprocessModel()
        {
            if (!assetPath.Contains("Resources/Models/")) return;
            var mi = (ModelImporter)assetImporter;
            mi.isReadable = true;            // colisores criados em tempo de jogo precisam ler a malha
            mi.importAnimation = false;
            mi.importCameras = false;
            mi.importLights = false;
            mi.importTangents = ModelImporterTangents.CalculateMikk;
            mi.meshCompression = ModelImporterMeshCompression.Off;
        }

        void OnPreprocessTexture()
        {
            bool model = assetPath.Contains("Resources/Models/");
            bool ground = assetPath.Contains("Resources/Textures/Chao/");
            if (model || ground)
            {
                var mt = (TextureImporter)assetImporter;
                string file = System.IO.Path.GetFileNameWithoutExtension(assetPath);
                bool normal = file.Contains("_nor");
                bool alpha = file.Contains("_alpha");
                mt.textureType = normal ? TextureImporterType.NormalMap : TextureImporterType.Default;
                mt.sRGBTexture = !normal && !alpha;
                mt.mipmapEnabled = true;
                mt.wrapMode = ground ? TextureWrapMode.Repeat : TextureWrapMode.Repeat;
                mt.anisoLevel = ground ? 8 : 4;
                mt.maxTextureSize = 1024;
                mt.textureCompression = TextureImporterCompression.Compressed;
                mt.isReadable = false;
                return;
            }
            if (assetPath.Contains("Resources/Textures/"))
            {
                // Pinceladas e papel: dados (não cor), repetem sem emenda.
                var tt = (TextureImporter)assetImporter;
                tt.textureType = TextureImporterType.Default;
                tt.sRGBTexture = System.IO.Path.GetFileName(assetPath).Contains("_diff"); // cor: sRGB; dados: linear
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
