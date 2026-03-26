using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;

namespace AIvisiontest.Tools
{
    public class LoadImages
    {
        // 方式1：加载本地图片文件
        public static BitmapImage LoadLocalImage(string imagePath)
        {
            var bitmapImage = new BitmapImage();
            if (File.Exists(imagePath))
            {

                bitmapImage.BeginInit();
                bitmapImage.UriSource = new Uri(imagePath, UriKind.Absolute);
                bitmapImage.CacheOption = BitmapCacheOption.OnLoad; // 防止文件被占用
                bitmapImage.EndInit();
            }

            return bitmapImage;

        }

        // 方式2：加载网络图片/内存流图片（如从摄像头采集的帧）
        public static BitmapImage LoadImageFromStream(Stream imageStream)
        {
            var bitmapImage = new BitmapImage();
            bitmapImage.BeginInit();
            bitmapImage.StreamSource = imageStream;
            bitmapImage.CacheOption = BitmapCacheOption.OnLoad;
            bitmapImage.EndInit();
            imageStream.Close(); // 按需关闭流

            return bitmapImage;
        }
        
    }
}
