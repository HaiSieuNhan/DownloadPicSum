using System;
using System.Net;
using System.Threading.Tasks;

namespace DownloadPicSum
{
    class Program
    {
        static async Task Main(string[] args)
        {
            Console.WriteLine("Hello World!");
           var xxx = new PicSumDequeue();

           await xxx.Start();

            while (true)
            {
                Console.ReadKey();
                Task.Delay(1000);
            }
        }
    }
}
