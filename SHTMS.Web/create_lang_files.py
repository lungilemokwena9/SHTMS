import json

languages = ['zu', 'xh', 'st', 'tn', 'nso', 'ts', 'ss', 've', 'nr']

with open('Resources/lang.en.json', 'r', encoding='utf-8') as f:
    en_data = json.load(f)

for code in languages:
    out_path = f'Resources/lang.{code}.json'
    with open(out_path, 'w', encoding='utf-8') as f:
        json.dump(en_data, f, ensure_ascii=False, indent=2)
    print(f'Created {out_path}')

print('Done! All 11 language files created.')