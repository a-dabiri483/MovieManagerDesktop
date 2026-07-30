import os

filepath = r'C:\Users\ALI\CascadeProjects\MovieManagerDesktop\Themes\PopUpMenu.xaml'
with open(filepath, 'r', encoding='utf-8') as f:
    content = f.read()

replacements = {
    'Header="Record"': 'Header="ضبط"',
    'Header="Reverse Playback"': 'Header="پخش معکوس"',
    'Header="Loop Playback"': 'Header="تکرار پخش"',
    'Header="Take a Snapshot"': 'Header="گرفتن عکس"',
    'Header="V.Sync"': 'Header="همگام‌سازی عمودی (V.Sync)"',
    'Header="Reset ..."': 'Header="بازنشانی ..."',
    'Header="Zoom In"': 'Header="بزرگنمایی"',
    'Header="Zoom out"': 'Header="کوچکنمایی"',
    'Header="Show Debug"': 'Header="نمایش اطلاعات رفع اشکال"',
    'Header="Exit"': 'Header="خروج"',
    'Header="لاین‌ها (Streams)"': 'Header="جریان‌ها (Streams)"', # Fix the persian text
}

for old, new in replacements.items():
    content = content.replace(old, new)

with open(filepath, 'w', encoding='utf-8') as f:
    f.write(content)
