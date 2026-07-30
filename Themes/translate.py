import re
import os

files = {
    'PopUpMenu.xaml': {
        r'Header="Embedded"': r'Header="داخلی"',
        r'Header="External"': r'Header="خارجی"',
        r'Header="Open File"': r'Header="باز کردن فایل"',
        r'Header="Paste Url"': r'Header="جایگذاری لینک"',
        r'Header="Audio"': r'Header="صدا"',
        r'Header="Enabled"': r'Header="فعال"',
        r'HeaderStringFormat="Delay \(\{0\}\)"': r'HeaderStringFormat="تأخیر ({0})"',
        r'Header="Reset\.\.\."': r'Header="بازنشانی..."',
        r'Header="Devices"': r'Header="دستگاه‌ها"',
        r'Header="Streams"': r'Header="لاین‌ها (Streams)"',
        r'Header="Subtitles"': r'Header="زیرنویس"',
        r'Header="Fonts\.\.\."': r'Header="فونت‌ها..."',
        r'HeaderStringFormat="Position Y \(\{0\}\)"': r'HeaderStringFormat="موقعیت عمودی ({0})"',
        r'Header="Up"': r'Header="بالا"',
        r'Header="Up x 10"': r'Header="بالا (سریع)"',
        r'Header="Down x 10"': r'Header="پایین (سریع)"',
        r'Header="Down"': r'Header="پایین"',
        r'Header="Search Local"': r'Header="جستجوی محلی"',
        r'Header="Search Online"': r'Header="جستجوی آنلاین"',
        r'Header="Video"': r'Header="ویدیو"',
        r'Header="Aspect Ratio"': r'Header="نسبت تصویر"',
        r'Header="Chapters"': r'Header="بخش‌ها (Chapters)"',
        r'Header="HW Acceleration"': r'Header="شتاب‌دهنده سخت‌افزاری"',
        r'Header="Speed"': r'Header="سرعت پخش"',
        r'Header="Zoom"': r'Header="بزرگ‌نمایی"',
        r'Header="Settings"': r'Header="تنظیمات پیشرفته"',
        r'Header="Stay on Top"': r'Header="نمایش روی تمام پنجره‌ها"',
        r'Header="Hide UI"': r'Header="مخفی کردن رابط کاربری"',
        r'Header="Play"': r'Header="پخش"',
        r'Header="Pause"': r'Header="توقف"',
        r'Header="Stop"': r'Header="ایست"',
        r'Header="Forward"': r'Header="جلو بردن"',
        r'Header="Backward"': r'Header="عقب بردن"',
        r'Header="Next"': r'Header="بعدی"',
        r'Header="Previous"': r'Header="قبلی"',
        r'Header="Mute"': r'Header="بی‌صدا"',
        r'Header="Fullscreen"': r'Header="تمام‌صفحه"',
        r'Header="Full Screen"': r'Header="تمام‌صفحه"',
    },
    'FlyleafBar.xaml': {
        r'ToolTip="Settings"': r'ToolTip="تنظیمات"',
        r'ToolTip="Fullscreen"': r'ToolTip="تمام‌صفحه"',
        r'ToolTip="Full Screen"': r'ToolTip="تمام‌صفحه"',
        r'ToolTip="Play"': r'ToolTip="پخش"',
        r'ToolTip="Pause"': r'ToolTip="توقف"',
        r'ToolTip="Mute"': r'ToolTip="بی‌صدا"',
        r'ToolTip="Unmute"': r'ToolTip="صدادار"',
        r'ToolTip="Subtitles"': r'ToolTip="زیرنویس"',
        r'ToolTip="Audio"': r'ToolTip="صدا"',
        r'ToolTip="Format"': r'ToolTip="فرمت"',
        r'ToolTip="Playlist"': r'ToolTip="لیست پخش"',
    }
}

for filename, translations in files.items():
    filepath = os.path.join(r'C:\Users\ALI\CascadeProjects\MovieManagerDesktop\Themes', filename)
    with open(filepath, 'r', encoding='utf-8') as f:
        content = f.read()
    
    for en, fa in translations.items():
        content = re.sub(en, fa, content)
        
    if filename == 'PopUpMenu.xaml':
        content = content.replace("<ContextMenu ", "<ContextMenu FlowDirection=\"RightToLeft\" ")
        content = content.replace("<ContextMenu>", "<ContextMenu FlowDirection=\"RightToLeft\">")
    
    with open(filepath, 'w', encoding='utf-8') as f:
        f.write(content)
print("Translation done.")
