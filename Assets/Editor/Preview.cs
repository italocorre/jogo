using System;
using System.IO;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace MarAberto.EditorTools
{
    /// <summary>
    /// Tira prints do jogo durante o build (no GitHub) para conferir o visual sem precisar abrir o jogo.
    /// </summary>
    public static class Preview
    {
        const int W = 1600, H = 900;

        public static void CaptureAll(string folder)
        {
            Directory.CreateDirectory(folder);
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var island = WorldBuilder.BuildIsland();
            var camGo = new GameObject("CameraPreview");
            var cam = camGo.AddComponent<Camera>();
            CameraSetup.Configure(cam);
            var envGo = new GameObject("Ambiente");
            var env = envGo.AddComponent<WorldEnvironment>();
            env.Init(cam);
            var ocean = WorldBuilder.BuildOcean(cam.transform);
            var boat = Boat.Spawn(island.BoatSpawn + Vector3.up * 0.1f, island.BoatSpawnRot);

            Vector3 spawnEye = island.SpawnPoint + Vector3.up * 0.6f;
            Vector3 c = island.transform.position;
            Vector3 dockDir = (island.DockEnd - island.SpawnPoint); dockDir.y = 0; dockDir.Normalize();
            Vector3 side = Vector3.Cross(Vector3.up, dockDir);

            void Shot(string name, Vector3 pos, Vector3 look, float tod)
            {
                try
                {
                    env.TimeOfDay = tod;
                    cam.transform.position = pos;
                    cam.transform.rotation = Quaternion.LookRotation(look - pos);
                    env.Tick(0f);
                    ocean.Follow();
                    var rt = new RenderTexture(W, H, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB) { antiAliasing = 4 };
                    cam.targetTexture = rt;
                    cam.Render();
                    cam.targetTexture = null;
                    var prev = RenderTexture.active;
                    RenderTexture.active = rt;
                    var tex = new Texture2D(W, H, TextureFormat.RGB24, false);
                    tex.ReadPixels(new Rect(0, 0, W, H), 0, 0);
                    tex.Apply();
                    RenderTexture.active = prev;
                    File.WriteAllBytes(Path.Combine(folder, name + ".jpg"), tex.EncodeToJPG(90));
                    UnityEngine.Object.DestroyImmediate(tex);
                    rt.Release();
                    UnityEngine.Object.DestroyImmediate(rt);
                    Debug.Log("[MarAberto] Print: " + name);
                }
                catch (Exception e)
                {
                    Debug.LogWarning("[MarAberto] Falhou print " + name + ": " + e);
                }
            }

            Shot("01_praia", spawnEye, spawnEye + island.SpawnLook * 10f + Vector3.down * 0.8f, 0.40f);
            Vector3 m = island.MerchantPos;
            Vector3 toSpawn = island.SpawnPoint - m; toSpawn.y = 0; toSpawn.Normalize();
            Shot("02_mercador", m + toSpawn * 6.5f + Vector3.up * 1.7f, m + Vector3.up * 1.3f, 0.42f);
            Vector3 sea = c + dockDir * 190f + side * 40f; sea.y = 3f;
            Shot("03_ilha_vista_do_mar", sea, c + Vector3.up * 12f, 0.62f);
            Vector3 dockEye = island.DockEnd + Vector3.up * 1.65f - dockDir * 3f - side * 0.6f;
            Shot("04_barco_no_pier", dockEye, boat.transform.position + Vector3.up * 0.3f, 0.45f);
            Vector3 under = c + dockDir * (island.Radius + 30f) - side * 25f; under.y = island.Height(under.x, under.z) + 2.5f;
            Shot("05_fundo_do_mar", under, under + dockDir * 12f + Vector3.down * 2f, 0.45f);
            Shot("06_por_do_sol", spawnEye, spawnEye + island.SpawnLook * 10f, 0.715f);
            Vector3 hill = c + side * 30f; hill.y = island.Height(hill.x, hill.z) + 1.7f;
            Shot("07_grama_e_arvores", hill, hill + (dockDir + side * 0.5f).normalized * 10f + Vector3.down * 1.5f, 0.38f);

            // Mãos em primeira pessoa
            var arm = Hands.BuildArm();
            var held = new GameObject("Picareta");
            held.AddComponent<MeshFilter>().sharedMesh = ItemModels.Get(ItemType.PicaretaPedra);
            held.AddComponent<MeshRenderer>().sharedMaterial = Mats.LowPoly;
            var root = new GameObject("Maos").transform;
            root.SetParent(cam.transform, false);
            root.localPosition = new Vector3(0.33f, -0.40f, 0.50f);
            root.localRotation = Quaternion.Euler(18f, -22f, 4f);
            arm.transform.SetParent(root, false);
            held.transform.SetParent(root, false);
            held.transform.localPosition = new Vector3(0, -0.02f, 0);
            held.transform.localScale = Vector3.one * 0.85f;
            Shot("08_primeira_pessoa", spawnEye, spawnEye + island.SpawnLook * 10f + Vector3.down * 1.5f, 0.40f);
            UnityEngine.Object.DestroyImmediate(root.gameObject);

            Shot("09_noite", spawnEye, spawnEye + island.SpawnLook * 10f + Vector3.up * 1f, 0.95f);

            // Folha com os ícones dos itens
            try
            {
                env.TimeOfDay = 0.45f;
                env.Tick(0f);
                var list = new System.Collections.Generic.List<Texture2D>();
                foreach (var d in Items.All) list.Add(Icons.Get(d.Type));
                var sheet = new Texture2D(128 * list.Count, 128, TextureFormat.RGBA32, false);
                var bg = new Color[128 * list.Count * 128];
                for (int i = 0; i < bg.Length; i++) bg[i] = new Color(0.12f, 0.18f, 0.22f, 1f);
                sheet.SetPixels(bg);
                for (int i = 0; i < list.Count; i++)
                {
                    var px = list[i].GetPixels();
                    for (int k = 0; k < px.Length; k++)
                    {
                        int x = i * 128 + k % 128, y = k / 128;
                        var b = sheet.GetPixel(x, y);
                        sheet.SetPixel(x, y, Color.Lerp(b, px[k], px[k].a));
                    }
                }
                sheet.Apply();
                File.WriteAllBytes(Path.Combine(folder, "10_icones.png"), sheet.EncodeToPNG());
            }
            catch (Exception e)
            {
                Debug.LogWarning("[MarAberto] Falhou ícones: " + e);
            }
        }
    }
}
