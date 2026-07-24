import os
import re

files_to_check = [
    r'C:\Users\ALI\CascadeProjects\MovieManagerDesktop\Services\IdentifyMediaService.cs',
    r'C:\Users\ALI\CascadeProjects\MovieManagerDesktop\Services\TvMazeService.cs',
    r'C:\Users\ALI\CascadeProjects\MovieManagerDesktop\Services\AnilistService.cs'
]

for filepath in files_to_check:
    with open(filepath, 'r', encoding='utf-8') as f:
        content = f.read()
    
    # IdentifyMediaService
    # res.PosterUrl = $"https://image.tmdb.org...
    content = re.sub(r'(res\.PosterUrl\s*=\s*)(.*?https://image\.tmdb\.org.*?;)', r'\1SettingsManager.WrapUrlWithProxy(\2);', content)
    
    content = re.sub(r'(file\.PosterUrl\s*=\s*)(.*?https://image\.tmdb\.org.*?;)', r'\1SettingsManager.WrapUrlWithProxy(\2);', content)
    content = re.sub(r'(file\.BackdropUrl\s*=\s*)(.*?https://image\.tmdb\.org.*?;)', r'\1SettingsManager.WrapUrlWithProxy(\2);', content)
    
    # TvMazeService
    content = re.sub(r'(result\.PosterUrl\s*=\s*)(origProp\.GetString\(\)\s*\?\?\s*\"\");', r'\1SettingsManager.WrapUrlWithProxy(\2);', content)
    content = re.sub(r'(result\.PosterUrl\s*=\s*)(medProp\.GetString\(\)\s*\?\?\s*\"\");', r'\1SettingsManager.WrapUrlWithProxy(\2);', content)
    
    # AnilistService
    content = re.sub(r'(result\.CoverImageUrl\s*=\s*)(el\.GetString\(\)\s*\?\?\s*\"\");', r'\1SettingsManager.WrapUrlWithProxy(\2);', content)
    content = re.sub(r'(result\.CoverImageUrl\s*=\s*)(l\.GetString\(\)\s*\?\?\s*\"\");', r'\1SettingsManager.WrapUrlWithProxy(\2);', content)
    content = re.sub(r'(result\.BannerImageUrl\s*=\s*)(banner\.GetString\(\)\s*\?\?\s*\"\");', r'\1SettingsManager.WrapUrlWithProxy(\2);', content)

    # Clean up double wrapping if it occurred
    content = content.replace('SettingsManager.WrapUrlWithProxy(SettingsManager.WrapUrlWithProxy(', 'SettingsManager.WrapUrlWithProxy(')
    content = content.replace('););', ');')
    
    with open(filepath, 'w', encoding='utf-8') as f:
        f.write(content)

print("Done")
