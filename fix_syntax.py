import os

filepath = r'C:\Users\ALI\CascadeProjects\MovieManagerDesktop\Services\IdentifyMediaService.cs'
with open(filepath, 'r', encoding='utf-8') as f:
    content = f.read()

content = content.replace('";);', '");')
content = content.replace('););', ');')

with open(filepath, 'w', encoding='utf-8') as f:
    f.write(content)

filepath = r'C:\Users\ALI\CascadeProjects\MovieManagerDesktop\Services\TvMazeService.cs'
with open(filepath, 'r', encoding='utf-8') as f:
    content = f.read()

content = content.replace('";);', '");')
content = content.replace('););', ');')

with open(filepath, 'w', encoding='utf-8') as f:
    f.write(content)

filepath = r'C:\Users\ALI\CascadeProjects\MovieManagerDesktop\Services\AnilistService.cs'
with open(filepath, 'r', encoding='utf-8') as f:
    content = f.read()

content = content.replace('";);', '");')
content = content.replace('););', ');')

with open(filepath, 'w', encoding='utf-8') as f:
    f.write(content)

print('Fixed!')
