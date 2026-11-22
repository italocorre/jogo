using UnityEngine;

namespace MarAberto
{
    /// <summary>
    /// Pós-processamento próprio (bloom, exposição, tonemapping ACES, cor e vinheta).
    /// É o que dá o visual "quente" e brilhante do jogo.
    /// </summary>
    [ExecuteAlways]
    [RequireComponent(typeof(Camera))]
    public class PostFX : MonoBehaviour
    {
        // Estilo "pintado à mão": contraste baixo, tom quente, sombras lilás e grão de papel.
        public float Threshold = 1.5f;
        public float Knee = 0.6f;
        public float BloomIntensity = 0.2f;
        public float Exposure = 0.95f;
        public float Saturation = 1.08f;
        public float Contrast = 0.98f;
        public float Vignette = 0.6f;
        public float Warmth = 0.05f;
        public Color Lift = new Color(0.016f, 0.008f, 0.026f);
        public float PaperGrain = 0.11f;

        Material mat;
        static readonly int paramsId = Shader.PropertyToID("_Params");
        static readonly int gradeId = Shader.PropertyToID("_Grade");
        static readonly int liftId = Shader.PropertyToID("_Lift");
        static readonly int bloomId = Shader.PropertyToID("_BloomTex");
        static readonly int paperId = Shader.PropertyToID("_Paper");
        static readonly int paperTexId = Shader.PropertyToID("_PaperTex");
        static Texture2D paperTex;
        const int Levels = 6;
        readonly RenderTexture[] chain = new RenderTexture[Levels];

        void OnRenderImage(RenderTexture src, RenderTexture dst)
        {
            if (mat == null)
            {
                var sh = Shader.Find("Hidden/MarAberto/Post");
                if (sh == null || !sh.isSupported)
                {
                    Graphics.Blit(src, dst);
                    return;
                }
                mat = new Material(sh) { hideFlags = HideFlags.HideAndDontSave };
            }

            mat.SetVector(paramsId, new Vector4(Threshold, Knee, BloomIntensity, Exposure));
            mat.SetVector(gradeId, new Vector4(Saturation, Contrast, Vignette, Warmth));
            mat.SetColor(liftId, Lift);
            if (paperTex == null) paperTex = Resources.Load<Texture2D>("Textures/papel");
            if (paperTex != null) mat.SetTexture(paperTexId, paperTex);
            float rep = src.height / 256f;
            mat.SetVector(paperId, new Vector4(paperTex != null ? PaperGrain : 0f, rep, src.width / (float)src.height, 0));

            int w = src.width / 2, h = src.height / 2;
            var fmt = RenderTextureFormat.DefaultHDR;
            int count = 0;
            RenderTexture prev = src;
            for (int i = 0; i < Levels; i++)
            {
                if (w < 4 || h < 4) break;
                chain[i] = RenderTexture.GetTemporary(w, h, 0, fmt);
                Graphics.Blit(prev, chain[i], mat, i == 0 ? 0 : 1);
                prev = chain[i];
                count++;
                w /= 2; h /= 2;
            }
            for (int i = count - 2; i >= 0; i--)
                Graphics.Blit(chain[i + 1], chain[i], mat, 2);

            mat.SetTexture(bloomId, count > 0 ? chain[0] : Texture2D.blackTexture);
            Graphics.Blit(src, dst, mat, 3);

            for (int i = 0; i < count; i++)
            {
                RenderTexture.ReleaseTemporary(chain[i]);
                chain[i] = null;
            }
        }
    }
}
