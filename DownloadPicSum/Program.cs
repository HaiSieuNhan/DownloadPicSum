using System;
using System.Diagnostics;
using System.Net;
using System.Threading.Tasks;

namespace DownloadPicSum
{
    class Program
    {
        static async Task Main(string[] args)
        {
            Console.WriteLine("Hello World!");
            var picSumDequeue = new PicSumDequeue();
            await picSumDequeue.Start();
            Console.ReadKey();
        }
    }
}
