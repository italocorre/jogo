# Baixa modelos e texturas realistas da Poly Haven (CC0) para Assets/Resources.
# Só pega o que o jogo usa: malha FBX + cor (diff) + relevo (nor_gl) + recorte (alpha), em 1k.
# Uso: python3 tools/baixar_modelos.py
import json, os, sys, urllib.request

ROOT = os.path.join(os.path.dirname(os.path.abspath(__file__)), '..', 'Assets', 'Resources')
UA = {'User-Agent': 'MarAberto-build/1.0'}

MODELS = [
    'picke_dirty_01', 'lambis_shell', 'stone_01', 'boulder_01', 'rock_moss_set_01', 'namaqualand_boulder_02',
    'modular_wooden_pier', 'treasure_chest', 'wooden_crate_01', 'wooden_lantern_01', 'wooden_barrels_01',
    'lateral_sea_marker', 'fern_02', 'pachira_aquatica_01',
    'wooden_axe_02', 'hatchet', 'wooden_hammer_01', 'metal_jerrycan', 'cannon_01', 'stone_fire_pit', 'wooden_bucket_01', 'dead_quiver_branch_01',
    # Itens reais (no lugar dos feitos à mão)
    'service_pistol', 'bolt_action_rifle_7_62', 'ammo_box', 'compost_bag_02', 'life_jacket', 'rubber_boots', 'small_lpg_tank',
    'old_gas_mask', 'vintage_flashlight', 'seeding_tray_01', 'sweet_potato', 'lemon', 'wooden_bowl_01', 'vintage_radio_transceiver',
    'food_avocado_01', 'planter_pot_clay', 'Barrel_01', 'vintage_suitcase', 'bronze_whale_statue', 'bronze_shark_statue', 'postcard_set_01',
]
# Texturas de chão (terreno): cor + relevo
TEXTURES = ['coast_sand_01', 'aerial_grass_rock', 'rock_06', 'forrest_ground_01', 'bark_brown_02', 'metal_plate']

KEEP = ('_diff_', '_nor_gl_', '_alpha_')


def get(url):
    with urllib.request.urlopen(urllib.request.Request(url, headers=UA), timeout=120) as r:
        return r.read()


def save(path, data):
    os.makedirs(os.path.dirname(path), exist_ok=True)
    with open(path, 'wb') as f:
        f.write(data)


def model(mid):
    files = json.loads(get('https://api.polyhaven.com/files/' + mid))
    fbx = files['fbx']['1k']['fbx']
    base = os.path.join(ROOT, 'Models', mid)
    save(os.path.join(base, os.path.basename(fbx['url'])), get(fbx['url']))
    for rel, info in fbx.get('include', {}).items():
        name = os.path.basename(rel)
        if not any(k in name for k in KEEP):
            continue
        if name.endswith('.exr'):
            # Relevo em EXR: baixa a versão PNG (a Unity lida melhor).
            alt = files.get('nor_gl', {}).get('1k', {}).get('png')
            if alt:
                save(os.path.join(base, rel[:-4] + '.png'), get(alt['url']))
                continue
        save(os.path.join(base, rel), get(info['url']))
    print('modelo', mid)


def texture(tid):
    files = json.loads(get('https://api.polyhaven.com/files/' + tid))
    base = os.path.join(ROOT, 'Textures', 'Chao')
    for key, out in (('Diffuse', '_diff'), ('nor_gl', '_nor')):
        entry = files.get(key, {}).get('1k', {})
        f = entry.get('jpg') or entry.get('png')
        if not f:
            continue
        ext = '.jpg' if f['url'].endswith('.jpg') else '.png'
        save(os.path.join(base, tid + out + ext), get(f['url']))
    print('textura', tid)


if __name__ == '__main__':
    only = sys.argv[1:]
    for m in MODELS:
        if not only or m in only:
            model(m)
    for t in TEXTURES:
        if not only or t in only:
            texture(t)
