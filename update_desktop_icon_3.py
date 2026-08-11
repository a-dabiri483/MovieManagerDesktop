import os
from PIL import Image

source_image_path = r'C:\Users\ALI\Downloads\Telegram Desktop\IMG_20260731_103939.png'
dest_png = r'C:\Users\ALI\CascadeProjects\MovieManagerDesktop\Assets\logo.png'
dest_ico = r'C:\Users\ALI\CascadeProjects\MovieManagerDesktop\Assets\logo.ico'

try:
    with Image.open(source_image_path) as img:
        img = img.convert("RGBA")
        
        # Save as PNG
        img.save(dest_png, 'PNG')
        
        # Save as ICO with multiple sizes for high quality Windows display
        icon_sizes = [(16, 16), (32, 32), (48, 48), (64, 64), (128, 128), (256, 256)]
        img.save(dest_ico, format='ICO', sizes=icon_sizes)
        
    print('Desktop icons updated successfully.')
except Exception as e:
    print('Error:', e)
