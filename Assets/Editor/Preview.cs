using System;
using System.IO;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace MarAberto.EditorTools
{
    /// <summary>
    /// Tira prints do jogo durante o build (no GitHub) para conferir o visual e a interface
    /// sem precisar abrir o jogo. Monta o jogo de verdade (Game.Setup) e fotografa vários lugares.
    /// </summary>
    public static class Preview
    {
        const int W = 1600, H = 900;

        public static void CaptureAll(string folder)
        {
            Directory.CreateDirectory(folder);
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var gameGo = new GameObject("Game");
            var game = gameGo.AddComponent<Game>();
            game.Setup(true);
            var island = game.StartIsland;
            var env = game.Environment;
            var cam = game.Player.Cam;
            var boat = game.GiveBoat();
            Icons.RenderAll();

            var rt = new RenderTexture(W, H, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB) { antiAliasing = 4 };
            var uiCamGo = new GameObject("CameraInterface");
            var uiCam = uiCamGo.AddComponent<Camera>();
            uiCam.clearFlags = CameraClearFlags.Depth;
            uiCam.cullingMask = 1 << Layers.UI;
            uiCam.orthographic = false;
            uiCam.nearClipPlane = 0.1f;
            uiCam.farClipPlane = 10f;
            uiCam.targetTexture = rt;
            uiCam.enabled = false;
            game.Hud.UseCamera(uiCam);
            var canvas = game.Hud.GetComponentInChildren<Canvas>(true);

            Vector3 spawnEye = island.SpawnPoint + Vector3.up * 0.6f;
            Vector3 c = island.transform.position;
            Vector3 dockDir = (island.DockEnd - island.SpawnPoint); dockDir.y = 0; dockDir.Normalize();
            Vector3 side = Vector3.Cross(Vector3.up, dockDir);

            void Shot(string name, Vector3 pos, Vector3 look, float tod, bool ui = false)
            {
                try
                {
                    env.TimeOfDay = tod;
                    cam.transform.position = pos;
                    cam.transform.rotation = Quaternion.LookRotation(look - pos);
                    env.Tick(0f);
                    game.Ocean.Follow();
                    cam.targetTexture = rt;
                    cam.Render();
                    cam.targetTexture = null;
                    if (ui)
                    {
                        canvas.gameObject.SetActive(true);
                        game.Hud.Refresh();
                        Canvas.ForceUpdateCanvases();
                        game.Hud.Refresh();
                        Canvas.ForceUpdateCanvases();
                        uiCam.Render();
                    }
                    var prev = RenderTexture.active;
                    RenderTexture.active = rt;
                    var tex = new Texture2D(W, H, TextureFormat.RGB24, false);
                    tex.ReadPixels(new Rect(0, 0, W, H), 0, 0);
                    tex.Apply();
                    RenderTexture.active = prev;
                    File.WriteAllBytes(Path.Combine(folder, name + ".jpg"), tex.EncodeToJPG(90));
                    UnityEngine.Object.DestroyImmediate(tex);
                    Debug.Log("[MarAberto] Print: " + name);
                }
                catch (Exception e)
                {
                    Debug.LogWarning("[MarAberto] Falhou print " + name + ": " + e);
                }
            }

            // Mundo (sem interface e sem as mãos)
            var hands = cam.transform.Find("Maos");
            if (hands != null) hands.gameObject.SetActive(false);
            Shot("01_praia", spawnEye, spawnEye + island.SpawnLook * 10f + Vector3.down * 0.8f, 0.40f);
            Vector3 m = island.MerchantPos;
            Vector3 toSpawn = island.SpawnPoint - m; toSpawn.y = 0; toSpawn.Normalize();
            Shot("02_mercador", m + toSpawn * 4.2f + Vector3.up * 1.65f, m + Vector3.up * 1.35f, 0.42f);
            Vector3 sea = c + dockDir * 430f + side * 90f; sea.y = 3f;
            Shot("03_ilha_vista_do_mar", sea, c + Vector3.up * 35f, 0.62f);
            Vector3 dockEye = island.DockEnd + Vector3.up * 1.65f - dockDir * 4f - side * 0.6f;
            Shot("04_barco", dockEye, boat.transform.position + Vector3.up * 0.2f, 0.45f);
            var reef = island.Reefs[0];
            Vector3 rc = reef.transform.position;
            Vector3 reefEye = rc + new Vector3(12f, 0, 8f); reefEye.y = reef.Height(reefEye.x, reefEye.z) + 3.5f;
            Shot("05_recife_fundo", reefEye, rc + Vector3.down * 14f, 0.45f);
            Shot("06_por_do_sol", spawnEye, spawnEye + island.SpawnLook * 10f, 0.715f);
            Vector3 hill = c + dockDir * 120f + side * 25f; hill.y = island.Height(hill.x, hill.z) + 1.7f;
            Shot("07_floresta_e_montanhas", hill, c + Vector3.up * 45f, 0.38f);
            Vector3 palmEye = island.SpawnPoint + Vector3.up * 0.6f - side * 3f;
            Shot("15_coqueiros", palmEye, palmEye - side * 10f + Vector3.up * 5f, 0.36f);
            // Mochila deixada ao desmaiar
            Vector3 bpPos = island.SpawnPoint + island.SpawnLook * 3f; bpPos.y = island.Height(bpPos.x, bpPos.z);
            var lost = LostBackpack.Spawn(bpPos, new[] { new ItemStack(ItemType.Cobre, 4) }, 20);
            Shot("16_mochila_perdida", bpPos - island.SpawnLook * 2.2f + Vector3.up * 1.3f + side * 0.5f, bpPos + Vector3.up * 0.25f, 0.42f);
            UnityEngine.Object.DestroyImmediate(lost.gameObject);
            Vector3 buoyView = rc + (c - rc).normalized * 22f + Vector3.up * 2.5f;
            Shot("08_boia_do_recife", buoyView, rc + Vector3.up * 1f, 0.5f);
            Shot("09_noite", spawnEye, spawnEye + island.SpawnLook * 10f + Vector3.up * 1f, 0.95f);

            // Interface
            if (hands != null) hands.gameObject.SetActive(true);
            var inv = game.Inventory;
            inv.Add(ItemType.Cobre, 12, false);
            inv.Add(ItemType.Carvao, 5, false);
            inv.Add(ItemType.Concha, 7, false);
            inv.Add(ItemType.Perola, 2, false);
            inv.Add(ItemType.Ferro, 3, false);
            inv.Add(ItemType.FragmentoMapa, 1, false);
            game.SetCoinsForPreview(187);
            game.Hands.ShowSelectedNow();
            game.Hud.Toast("Cobre", Hud.Info, ItemType.Cobre, 3);
            game.Hud.Toast("Âncora presa no fundo (12 m). Pode mergulhar!", Hud.Good);
            Shot("10_interface", spawnEye, spawnEye + island.SpawnLook * 10f + Vector3.down * 1.2f, 0.42f, true);
            game.Hud.PreviewPanel(1);
            Shot("11_mochila", spawnEye, spawnEye + island.SpawnLook * 10f, 0.42f, true);
            var merchant = UnityEngine.Object.FindAnyObjectByType<Merchant>();
            game.Hud.PreviewPanel(2, merchant);
            Shot("12_loja", m + toSpawn * 4.2f + Vector3.up * 1.65f, m + Vector3.up * 1.35f, 0.42f, true);
            game.Hud.PreviewPanel(3);
            Shot("13_pausa", spawnEye, spawnEye + island.SpawnLook * 10f, 0.42f, true);
            game.Hud.PreviewPanel(0);

            // Folha com os ícones dos itens
            try
            {
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
                File.WriteAllBytes(Path.Combine(folder, "14_icones.png"), sheet.EncodeToPNG());
            }
            catch (Exception e)
            {
                Debug.LogWarning("[MarAberto] Falhou ícones: " + e);
            }
        }
    }
}
