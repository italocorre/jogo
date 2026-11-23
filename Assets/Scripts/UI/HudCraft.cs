using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace MarAberto
{
    /// <summary>Aba FABRICAR do Tab: categorias, receitas, pesquisa e fabricação.</summary>
    public partial class Hud
    {
        RectTransform craftView, craftCats, craftGrid, craftDetail;
        CraftCategory craftCat = CraftCategory.Ferramentas;
        Recipe craftSel;
        int craftVersion = -1, craftDirty;
        readonly List<(Text label, Image bg, CraftCategory cat)> catButtons = new List<(Text, Image, CraftCategory)>();

        RectTransform craftContent;
        ScrollRect craftScroll;

        /// <summary>Ícone de cada categoria (item de verdade).</summary>
        static ItemType CatIcon(CraftCategory c)
        {
            switch (c)
            {
                case CraftCategory.Ferramentas: return ItemType.MachadoPedra;
                case CraftCategory.Armas: return ItemType.Pistola;
                case CraftCategory.Construcao: return ItemType.Madeira;
                case CraftCategory.Jangada: return ItemType.Corda;
                case CraftCategory.Maquinas: return ItemType.LingoteFerro;
                case CraftCategory.Veiculos: return ItemType.Gasolina;
                case CraftCategory.Navegacao: return ItemType.Receptor;
                case CraftCategory.Comida: return ItemType.PeixeAssado;
                default: return ItemType.Mochila;
            }
        }

        /// <summary>Fabricar (B): coluna no lado direito, como no Stranded Deep — categorias em cima, lista no meio, detalhe embaixo.</summary>
        void BuildCraft(RectTransform body)
        {
            craftView = UIKit.Rect(body, "VistaFabricar").Fill();
            craftCats = UIKit.Rect(craftView, "Categorias").Place(new Vector2(0, 1), new Vector2(0, 1), Vector2.zero, new Vector2(600, 96));
            int i = 0;
            foreach (CraftCategory c in System.Enum.GetValues(typeof(CraftCategory)))
            {
                var cat = c;
                var bg = UIKit.Img(craftCats, "Cat" + c, UIKit.Slot, new Color(1, 1, 1, 0.5f), true, 0.6f);
                bg.raycastTarget = true;
                bg.rectTransform.Place(new Vector2(0, 1), new Vector2(0, 1), new Vector2(i * 66, 0), new Vector2(60, 60));
                var b = bg.gameObject.AddComponent<Button>();
                b.transition = Selectable.Transition.None;
                b.onClick.AddListener(() => { craftCat = cat; craftSel = null; Sfx.UI(Sfx.Page, 0.4f); craftDirty++; if (craftScroll != null) craftScroll.verticalNormalizedPosition = 1f; });
                var ic = UIKit.Icon(bg.transform, "Icone");
                ic.rectTransform.Fill(7, 7, 7, 7);
                ic.texture = Icons.Get(CatIcon(c));
                var t = UIKit.Txt(bg.transform, "Texto", "", UIKit.BodyBold, 11, UIKit.Muted, TextAnchor.UpperCenter);
                t.rectTransform.Place(new Vector2(0.5f, 0), new Vector2(0.5f, 1), new Vector2(0, -2), new Vector2(80, 30));
                catButtons.Add((t, bg, c));
                i++;
            }
            // Lista com rolagem (roda do mouse).
            var view = UIKit.Rect(craftView, "Lista").Place(new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, -100), new Vector2(600, 400));
            view.gameObject.AddComponent<RectMask2D>();
            var hit = view.gameObject.AddComponent<Image>();
            hit.color = new Color(0, 0, 0, 0.001f);
            craftContent = UIKit.Rect(view, "Conteudo").Place(new Vector2(0, 1), new Vector2(0, 1), Vector2.zero, new Vector2(600, 400));
            craftScroll = view.gameObject.AddComponent<ScrollRect>();
            craftScroll.content = craftContent;
            craftScroll.horizontal = false;
            craftScroll.movementType = ScrollRect.MovementType.Clamped;
            craftScroll.scrollSensitivity = 40f;
            craftGrid = craftContent;
            UIKit.Img(craftView, "Fio2", UIKit.White, new Color(UIKit.Brass.r, UIKit.Brass.g, UIKit.Brass.b, 0.3f)).rectTransform
                .Place(new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, -508), new Vector2(600, 1));
            craftDetail = UIKit.Rect(craftView, "Detalhe").Place(new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, -516), new Vector2(600, 400));
            Crafting.OnChanged += () => craftDirty++;
        }

        /// <summary>Abre o Tab já na aba FABRICAR (usado nos prints).</summary>
        public void PreviewCraft(CraftCategory cat)
        {
            SetPanel(Panel.Backpack, false);
            tab = Tab.Craft;
            craftCat = cat;
            craftSel = null;
            RefreshTabs();
            RefreshCraft();
        }

        int CraftStamp() => craftDirty * 7919 + invVersion * 31 + (int)craftCat + (craftSel != null ? craftSel.Id.GetHashCode() : 0);

        void RefreshCraft()
        {
            int stamp = CraftStamp();
            if (stamp == craftVersion) return;
            craftVersion = stamp;
            var inv = game.Inventory;

            foreach (var (label, bg, cat) in catButtons)
            {
                bool on = cat == craftCat;
                int ready = 0;
                foreach (var r in Crafting.All)
                    if (r.Category == cat && (Crafting.CanCraft(inv, r, out _) || Crafting.CanResearch(inv, r))) ready++;
                label.text = on ? Crafting.CategoryName(cat).ToUpperInvariant() : (ready > 0 ? "<color=#8FE08A>•</color>" : "");
                label.color = on ? UIKit.Gold : UIKit.Muted;
                bg.color = on ? new Color(1f, 0.85f, 0.5f, 0.9f) : new Color(1, 1, 1, 0.45f);
            }

            // Grade de receitas da categoria
            UIKit.ClearChildren(craftGrid);
            var list = new List<Recipe>();
            foreach (var r in Crafting.All) if (r.Category == craftCat) list.Add(r);
            if (craftSel == null || craftSel.Category != craftCat) craftSel = list.Count > 0 ? list[0] : null;
            const float rowH = 66, gap = 6;
            craftContent.sizeDelta = new Vector2(600, Mathf.Max(400, list.Count * (rowH + gap)));
            for (int i = 0; i < list.Count; i++)
            {
                var r = list[i];
                bool coming = Crafting.IsComing(r), unlocked = Crafting.IsUnlocked(r);
                bool can = Crafting.CanCraft(inv, r, out _), canRes = Crafting.CanResearch(inv, r);
                var card = UIKit.Img(craftGrid, "Receita", r == craftSel ? UIKit.SlotSelected : UIKit.Slot, null, true, 0.7f);
                card.raycastTarget = true;
                card.rectTransform.Place(new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, -i * (rowH + gap)), new Vector2(590, rowH));
                if (coming) card.color = new Color(1, 1, 1, 0.45f);
                var btn = card.gameObject.AddComponent<Button>();
                btn.transition = Selectable.Transition.None;
                var rr = r;
                btn.onClick.AddListener(() => { craftSel = rr; Sfx.UI(Sfx.Page, 0.3f); craftDirty++; });
                var ic = UIKit.Icon(card.transform, "Icone");
                ic.rectTransform.Place(new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(8, 0), new Vector2(56, 56));
                ic.texture = Icons.ForRecipe(r);
                ic.enabled = ic.texture != null;
                ic.color = coming ? new Color(0.4f, 0.4f, 0.4f, 0.6f) : unlocked ? Color.white : new Color(0.55f, 0.55f, 0.6f, 0.9f);
                var nm = UIKit.Txt(card.transform, "Nome", r.Name, UIKit.BodyBold, 17, coming ? UIKit.Muted : UIKit.Text, TextAnchor.UpperLeft);
                nm.rectTransform.Place(new Vector2(0, 1), new Vector2(0, 1), new Vector2(74, -8), new Vector2(230, 26));
                string tag = coming ? "EM BREVE" : !unlocked ? (canRes ? "PESQUISAR" : "BLOQUEADA") : can ? "FABRICAR" : "";
                Color tc = coming ? UIKit.Muted : !unlocked ? (canRes ? UIKit.Gold : UIKit.BadC) : UIKit.GoodC;
                UIKit.Txt(card.transform, "Estado", tag, UIKit.Title, 12, tc, TextAnchor.LowerLeft)
                    .rectTransform.Place(new Vector2(0, 0), new Vector2(0, 0), new Vector2(74, 8), new Vector2(200, 18));
                // Ingredientes em quadradinhos (como no Stranded Deep).
                var cost = unlocked ? r.Cost : r.Research;
                for (int k = 0; k < cost.Length && k < 5; k++)
                {
                    var (ct, cn) = cost[k];
                    var cell = UIKit.Img(card.transform, "Ing", UIKit.Slot, new Color(1, 1, 1, 0.55f), true, 0.5f);
                    cell.rectTransform.Place(new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(-8 - (Mathf.Min(cost.Length, 5) - 1 - k) * 52, 0), new Vector2(48, 48));
                    var ci = UIKit.Icon(cell.transform, "Icone");
                    ci.rectTransform.Fill(5, 5, 5, 5);
                    ci.texture = Icons.Get(ct);
                    bool has = inv.Count(ct) >= cn;
                    UIKit.Txt(cell.transform, "Qtd", cn.ToString(), UIKit.BodyBold, 13, has ? Color.white : UIKit.BadC, TextAnchor.LowerRight)
                        .rectTransform.Fill(2, 1, 4, 1);
                }
            }

            // Detalhe da receita
            UIKit.ClearChildren(craftDetail);
            if (craftSel == null) return;
            var sel = craftSel;
            bool sComing = Crafting.IsComing(sel), sUnlocked = Crafting.IsUnlocked(sel);
            var big = UIKit.Img(craftDetail, "Quadro", UIKit.Slot, null, true, 1f);
            big.rectTransform.Place(new Vector2(0, 1), new Vector2(0, 1), new Vector2(20, 0), new Vector2(120, 120));
            var bic = UIKit.Icon(big.transform, "Icone");
            bic.rectTransform.Fill(12, 12, 12, 12);
            bic.texture = Icons.ForRecipe(sel);
            bic.enabled = bic.texture != null;
            UIKit.Txt(craftDetail, "Nome", sel.Name, UIKit.Title, 26, UIKit.Cream, TextAnchor.UpperLeft)
                .rectTransform.Place(new Vector2(0, 1), new Vector2(0, 1), new Vector2(156, -6), new Vector2(240, 70));
            UIKit.Txt(craftDetail, "Cat", Crafting.CategoryName(sel.Category).ToUpperInvariant(), UIKit.BodyBold, 14, UIKit.Label, TextAnchor.UpperLeft)
                .rectTransform.Place(new Vector2(0, 1), new Vector2(0, 1), new Vector2(156, -86), new Vector2(240, 22));
            UIKit.Txt(craftDetail, "Desc", sel.Description, UIKit.Body, 19, UIKit.Text, TextAnchor.UpperLeft)
                .rectTransform.Place(new Vector2(0, 1), new Vector2(0, 1), new Vector2(20, -128), new Vector2(560, 60));

            float y = -224;
            if (sComing)
            {
                UIKit.Txt(craftDetail, "EmBreve", "Chega na " + sel.ComingIn + " do desenvolvimento.", UIKit.BodyBold, 19, UIKit.Muted, TextAnchor.UpperLeft)
                    .rectTransform.Place(new Vector2(0, 1), new Vector2(0, 1), new Vector2(20, y), new Vector2(370, 60));
                return;
            }
            if (!sUnlocked)
            {
                y = CostList(craftDetail, "PESQUISA  (uma vez, libera para sempre)", sel.Research, y);
                var rb = UIKit.Btn(craftDetail, "Pesquisar", "PESQUISAR", () =>
                {
                    if (Crafting.DoResearch(game.Inventory, sel)) { Sfx.UI(Sfx.Coin, 0.6f); Toast("Receita aprendida: " + sel.Name, Good); }
                    else Sfx.UI(Sfx.Error, 0.4f);
                }, 22);
                rb.interactable = Crafting.CanResearch(inv, sel);
                ((RectTransform)rb.transform).Place(new Vector2(0, 1), new Vector2(0, 1), new Vector2(20, y - 10), new Vector2(360, 54));
                y -= 80;
            }
            y = CostList(craftDetail, "FABRICAR", sel.Cost, y);
            bool ok = Crafting.CanCraft(inv, sel, out string why);
            var cb = UIKit.Btn(craftDetail, "Fabricar", ok ? "FABRICAR" : (why ?? "").ToUpperInvariant(), () =>
            {
                if (Crafting.DoCraft(game.Inventory, sel)) Sfx.UI(Sfx.Pickup, 0.6f);
                else Sfx.UI(Sfx.Error, 0.4f);
            }, 20);
            cb.interactable = ok;
            ((RectTransform)cb.transform).Place(new Vector2(0, 1), new Vector2(0, 1), new Vector2(20, y - 10), new Vector2(360, 54));
        }

        float CostList(RectTransform parent, string title, (ItemType type, int count)[] cost, float y)
        {
            UIKit.Txt(parent, "Titulo", title, UIKit.Title, 15, UIKit.Gold, TextAnchor.UpperLeft)
                .rectTransform.Place(new Vector2(0, 1), new Vector2(0, 1), new Vector2(20, y), new Vector2(370, 22));
            y -= 28;
            var inv = game.Inventory;
            foreach (var (t, n) in cost)
            {
                int have = inv.Count(t);
                var row = UIKit.Rect(parent, "Linha").Place(new Vector2(0, 1), new Vector2(0, 1), new Vector2(20, y), new Vector2(370, 36));
                var ic = UIKit.Icon(row, "Icone");
                ic.rectTransform.Place(new Vector2(0, 0.5f), new Vector2(0, 0.5f), Vector2.zero, new Vector2(34, 34));
                ic.texture = Icons.Get(t);
                UIKit.Txt(row, "Nome", Items.Get(t).Name, UIKit.Body, 19, UIKit.Text, TextAnchor.MiddleLeft)
                    .rectTransform.Place(new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(44, 0), new Vector2(220, 30));
                UIKit.Txt(row, "Qtd", have + " / " + n, UIKit.BodyBold, 19, have >= n ? UIKit.GoodC : UIKit.BadC, TextAnchor.MiddleRight)
                    .rectTransform.Place(new Vector2(1, 0.5f), new Vector2(1, 0.5f), Vector2.zero, new Vector2(110, 30));
                y -= 38;
            }
            if (cost.Length == 0)
            {
                UIKit.Txt(parent, "Nada", "Grátis", UIKit.Body, 18, UIKit.Muted, TextAnchor.UpperLeft)
                    .rectTransform.Place(new Vector2(0, 1), new Vector2(0, 1), new Vector2(20, y), new Vector2(370, 26));
                y -= 30;
            }
            return y - 6;
        }
    }
}
