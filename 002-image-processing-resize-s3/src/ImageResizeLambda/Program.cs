using SkiaSharp;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ImageResizeLambda
{
    public class Program
    {
        public static void Main(string[] args)
        {
            using var fileStream = new FileStream("C:\\Users\\VitaliiSamarskyi\\Downloads\\3.png", FileMode.Open, FileAccess.Read);
            using var bitmap = SKBitmap.Decode(fileStream);
            if (bitmap == null)
            {
                Console.WriteLine("Error decoding object.");
                return;
            }
            Console.WriteLine("Hello World!");
        }
    }
}
