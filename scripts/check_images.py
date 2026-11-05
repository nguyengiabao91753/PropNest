from PIL import Image
import os
import sys

sys.stdout.reconfigure(encoding='utf-8')

images = [
    'docs/diagrams/hinh_2_1_kien_truc_tong_the.png',
    'docs/diagrams/hinh_2_3_saga_orchestration.png',
    'docs/diagrams/hinh_2_4_transactional_outbox.png',
    'docs/diagrams/hinh_2_5_optimistic_concurrency.png',
    'docs/screenshots/swagger_ui.png',
    'docs/screenshots/ui_seller_wallet.png'
]

for p in images:
    if os.path.exists(p):
        im = Image.open(p)
        print(f"{p}: format={im.format}, size={im.size}, mode={im.mode}")
    else:
        print(f"Missing: {p}")
