import re
import os

files = ['FlyleafBar.xaml', 'FlyleafME.xaml', 'PopUpMenu.xaml']

for file in files:
    print('--- ' + file + ' ---')
    filepath = os.path.join(r'C:\Users\ALI\CascadeProjects\MovieManagerDesktop\Themes', file)
    with open(filepath, 'r', encoding='utf-8') as f:
        lines = f.readlines()
        
    for i, line in enumerate(lines):
        # Find any Text="..." or Header="..." or ToolTip="..."
        matches = re.findall(r'(Text|Header|ToolTip|HeaderStringFormat)=\"([^\"]*[a-zA-Z]+[^\"]*)\"', line)
        for attr, val in matches:
            if '{' in val or 'x:Static' in val:
                continue
            if not re.search(r'[a-zA-Z]', val):
                continue
            
            print(f'Line {i+1}: {attr}="{val}"')
