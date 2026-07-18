using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;

namespace MovieManagerDesktop.Services
{
    public class IconConverterService
    {
        // تبدیل تصویر به فرمت ICO چند رزولوشن
        public static string ConvertToIcon(string imagePath, string outputPath)
        {
            using (var sourceImage = Image.FromFile(imagePath))
            {
                return ConvertImageToIcon(sourceImage, outputPath);
            }
        }

        // تبدیل تصویر ترکیب شده با قالب به فرمت ICO
        public static string CreateTemplateIcon(string posterPath, string templatePath, string outputPath, double? rating = null)
        {
            using (var templateImg = Image.FromFile(templatePath))
            using (var posterImg = Image.FromFile(posterPath))
            using (var compositeImg = new Bitmap(templateImg.Width, templateImg.Height))
            {
                using (var g = Graphics.FromImage(compositeImg))
                {
                    g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
                    g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;

                    // 1. رسم پوستر در لایه زیرین
                    // مختصات و ابعاد تنظیم شده برای قالب dvdbox
                    int pX = (int)(templateImg.Width * 0.20);
                    int pY = (int)(templateImg.Height * 0.04);
                    int pWidth = (int)(templateImg.Width * 0.76);
                    int pHeight = (int)(templateImg.Height * 0.92);
                    g.DrawImage(posterImg, pX, pY, pWidth, pHeight);
                    
                    // 2. رسم قاب پوشه (که قسمت شیشه‌ای/شفاف دارد) در لایه رویی
                    g.DrawImage(templateImg, 0, 0, templateImg.Width, templateImg.Height);
                    
                    if (rating.HasValue && rating.Value > 0)
                    {
                        DrawRatingBadge(g, templateImg.Width, templateImg.Height);
                        DrawStarAndScore(g, templateImg.Width, templateImg.Height, rating.Value);
                    }
                }
                
                return ConvertImageToIcon(compositeImg, outputPath);
            }
        }

        private static void DrawRatingBadge(Graphics g, int width, int height)
        {
            int rectWidth = (int)(width * 0.28);
            int rectHeight = (int)(height * 0.12);
            int rectX = (int)(width * 0.22); // کمی بعد از لبه سمت چپ پوستر
            int rectY = height - rectHeight - (int)(height * 0.08);
            
            using (var brush = new SolidBrush(Color.FromArgb(40, 40, 140))) // رنگ سرمه ای تیره
            {
                g.FillRectangle(brush, rectX, rectY, rectWidth, rectHeight);
            }
            
            using (var font = new Font("Arial", rectHeight * 0.45f, FontStyle.Bold))
            using (var textBrush = new SolidBrush(Color.White))
            {
                var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
                g.DrawString("TV-14", font, textBrush, new RectangleF(rectX, rectY, rectWidth, rectHeight), sf);
            }
        }

        private static void DrawStarAndScore(Graphics g, int width, int height, double rating)
        {
            int starSize = (int)(width * 0.25);
            int starX = width - starSize - (int)(width * 0.02);
            int starY = height - starSize - (int)(height * 0.02);
            
            PointF[] points = new PointF[10];
            double angle = -Math.PI / 2;
            float cx = starX + starSize / 2f;
            float cy = starY + starSize / 2f;
            float outerRadius = starSize / 2f;
            float innerRadius = outerRadius * 0.4f;
            
            for (int i = 0; i < 10; i++)
            {
                float r = (i % 2 == 0) ? outerRadius : innerRadius;
                points[i] = new PointF(
                    cx + (float)(Math.Cos(angle) * r),
                    cy + (float)(Math.Sin(angle) * r)
                );
                angle += Math.PI / 5;
            }
            
            // سایه ستاره
            using (var shadowBrush = new SolidBrush(Color.FromArgb(100, 0, 0, 0)))
            {
                var shadowPoints = new PointF[10];
                for (int i=0; i<10; i++) shadowPoints[i] = new PointF(points[i].X + 3, points[i].Y + 3);
                g.FillPolygon(shadowBrush, shadowPoints);
            }

            using (var brush = new SolidBrush(Color.FromArgb(253, 203, 88))) // رنگ طلایی ستاره
            {
                g.FillPolygon(brush, points);
            }
            
            using (var font = new Font("Arial", starSize * 0.32f, FontStyle.Bold))
            using (var textBrush = new SolidBrush(Color.Black))
            {
                var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
                g.DrawString(rating.ToString("0.0"), font, textBrush, new RectangleF(starX, starY + (starSize * 0.05f), starSize, starSize), sf);
            }
        }

        private static string ConvertImageToIcon(Image sourceImage, string outputPath)
        {
            using (var iconStream = new FileStream(outputPath, FileMode.Create))
            {
                var sizes = new[] { 256, 128, 64, 48, 32, 16 };
                
                // نوشتن هدر ICO
                iconStream.WriteByte(0);  // Reserved
                iconStream.WriteByte(0);  // Reserved
                iconStream.WriteByte(1);  // Type (1 = ICO)
                iconStream.WriteByte(0);  // Type
                iconStream.WriteByte((byte)sizes.Length);  // Image count
                iconStream.WriteByte(0);  // Image count
                
                // محاسبه آفست‌ها
                int dataOffset = 6 + (sizes.Length * 16);
                var imageEntries = new (int offset, int size)[sizes.Length];
                
                // نوشتن directory entries
                for (int i = 0; i < sizes.Length; i++)
                {
                    int size = sizes[i];
                    
                    // محاسبه دقیق سایز: BMP header + pixel data + AND mask
                    var bmpHeaderSize = 40;
                    var pixelDataSize = size * size * 4; // 32 bits per pixel
                    var andMaskRowSize = ((size + 31) / 32) * 4; // 4-byte aligned rows
                    var andMaskSize = andMaskRowSize * size;
                    var totalSize = bmpHeaderSize + pixelDataSize + andMaskSize;
                    
                    imageEntries[i] = (dataOffset, totalSize);
                    dataOffset += totalSize;
                    
                    // نوشتن directory entry
                    iconStream.WriteByte((byte)size);  // Width
                    iconStream.WriteByte((byte)size);  // Height
                    iconStream.WriteByte(0);  // Color count
                    iconStream.WriteByte(0);  // Reserved
                    iconStream.WriteByte(1);  // Color planes
                    iconStream.WriteByte(0);  // Color planes
                    iconStream.WriteByte(32); // Bits per pixel
                    iconStream.WriteByte(0);  // Bits per pixel
                    
                    // نوشتن سایز
                    iconStream.WriteByte((byte)(totalSize & 0xFF));
                    iconStream.WriteByte((byte)((totalSize >> 8) & 0xFF));
                    iconStream.WriteByte((byte)((totalSize >> 16) & 0xFF));
                    iconStream.WriteByte((byte)((totalSize >> 24) & 0xFF));
                    
                    // نوشتن آفست
                    iconStream.WriteByte((byte)(imageEntries[i].offset & 0xFF));
                    iconStream.WriteByte((byte)((imageEntries[i].offset >> 8) & 0xFF));
                    iconStream.WriteByte((byte)((imageEntries[i].offset >> 16) & 0xFF));
                    iconStream.WriteByte((byte)((imageEntries[i].offset >> 24) & 0xFF));
                }
                
                // نوشتن داده‌های تصاویر
                foreach (int size in sizes)
                {
                    using (var bitmap = new Bitmap(size, size))
                    using (var graphics = Graphics.FromImage(bitmap))
                    {
                        graphics.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
                        graphics.DrawImage(sourceImage, 0, 0, size, size);
                        
                        // قفل کردن بیت‌مپ برای دسترسی مستقیم به پیکسل‌ها
                        var bmpData = bitmap.LockBits(
                            new Rectangle(0, 0, bitmap.Width, bitmap.Height),
                            ImageLockMode.ReadOnly,
                            PixelFormat.Format32bppArgb
                        );
                        
                        try
                        {
                            // نوشتن BMP header
                            iconStream.Write(new byte[] { 40, 0, 0, 0 }, 0, 4); // Header size
                            iconStream.Write(BitConverter.GetBytes(bitmap.Width), 0, 4);
                            iconStream.Write(BitConverter.GetBytes(bitmap.Height * 2), 0, 4); // Height * 2 for ICO
                            iconStream.Write(new byte[] { 1, 0 }, 0, 2); // Planes
                            iconStream.Write(new byte[] { 32, 0 }, 0, 2); // Bits per pixel
                            iconStream.Write(new byte[] { 0, 0, 0, 0 }, 0, 4); // Compression
                            iconStream.Write(BitConverter.GetBytes(bitmap.Width * bitmap.Height * 4), 0, 4); // Image size
                            iconStream.Write(BitConverter.GetBytes(0), 0, 4); // X pixels per meter
                            iconStream.Write(BitConverter.GetBytes(0), 0, 4); // Y pixels per meter
                            iconStream.Write(BitConverter.GetBytes(0), 0, 4); // Colors used
                            iconStream.Write(BitConverter.GetBytes(0), 0, 4); // Colors important
                            
                            // نوشتن داده‌های پیکسل در bottom-up order
                            int stride = Math.Abs(bmpData.Stride);
                            int pixelDataSize = stride * bitmap.Height;
                            byte[] pixelData = new byte[pixelDataSize];
                            
                            Marshal.Copy(bmpData.Scan0, pixelData, 0, pixelDataSize);
                            
                            // نوشتن از پایین به بالا (Windows ICO requirement)
                            for (int y = bitmap.Height - 1; y >= 0; y--)
                            {
                                int rowStart = y * stride;
                                iconStream.Write(pixelData, rowStart, stride);
                            }
                            
                            // نوشتن AND mask با 4-byte alignment
                            var andMaskRowSize = ((bitmap.Width + 31) / 32) * 4;
                            var andMaskSize = andMaskRowSize * bitmap.Height;
                            var andMask = new byte[andMaskSize];
                            iconStream.Write(andMask, 0, andMask.Length);
                        }
                        finally
                        {
                            bitmap.UnlockBits(bmpData);
                        }
                    }
                }
            }
            
            return outputPath;
        }
    }
}
