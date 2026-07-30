import re
filepath = r'C:\Users\ALI\CascadeProjects\MovieManagerDesktop\Themes\FlyleafBar.xaml'
with open(filepath, 'r', encoding='utf-8') as f:
    content = f.read()

replacements = {
    'Command="{Binding Player.Commands.TogglePlayPause}"': 'Command="{Binding Player.Commands.TogglePlayPause}" ToolTip="پخش / توقف"',
    'Command="{Binding OpenContextMenu}" CommandParameter="{Binding RelativeSource={RelativeSource Self}}" Grid.Column="1"': 'Command="{Binding OpenContextMenu}" CommandParameter="{Binding RelativeSource={RelativeSource Self}}" Grid.Column="1" ToolTip="زیرنویس"',
    'Command="{Binding Player.Commands.Reopen}" CommandParameter="{Binding Player.Playlist.PrevItem}"': 'Command="{Binding Player.Commands.Reopen}" CommandParameter="{Binding Player.Playlist.PrevItem}" ToolTip="قبلی"',
    'Command="{Binding Player.Commands.Reopen}" CommandParameter="{Binding Player.Playlist.NextItem}"': 'Command="{Binding Player.Commands.Reopen}" CommandParameter="{Binding Player.Playlist.NextItem}" ToolTip="بعدی"',
    'Command="{Binding Player.Commands.ToggleMute}"': 'Command="{Binding Player.Commands.ToggleMute}" ToolTip="قطع / وصل صدا"',
    'Command="{Binding OpenSettingsCmd}"': 'Command="{Binding OpenSettingsCmd}" ToolTip="تنظیمات"',
    'Content="{materialDesign:PackIcon Kind=Fullscreen, Size=28}"': 'Content="{materialDesign:PackIcon Kind=Fullscreen, Size=28}" ToolTip="تمام صفحه"'
}

for old, new in replacements.items():
    content = content.replace(old, new)

content = content.replace('Command="{Binding OpenContextMenu}" CommandParameter="{Binding RelativeSource={RelativeSource Self}}">', 'Command="{Binding OpenContextMenu}" CommandParameter="{Binding RelativeSource={RelativeSource Self}}" ToolTip="تنظیمات ویدیو">')

with open(filepath, 'w', encoding='utf-8') as f:
    f.write(content)
