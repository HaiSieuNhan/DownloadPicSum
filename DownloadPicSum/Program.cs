using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;
using SkiaSharp;

namespace DownloadPicSum
{
    internal static class Program
    {
        private static async Task Main(string[] args)
        {
            Console.WriteLine("Hello World!");
            await new PicSumDequeue().Start();
            Console.ReadKey();
        }

        //static async Task Main(string[] args)
        //{
        //    var lstStr = new List<string>()
        //    {
        //        "Dao","Van","Hai","Ahihi","1","2","3","4"
        //    };

        //    ActionBlock<string> actionBlock = new ActionBlock<string>(async (input) =>
        //    {
        //        Console.WriteLine(input +" : ThreaId => " + Thread.CurrentThread.ManagedThreadId);

        //    }, new ExecutionDataflowBlockOptions { MaxDegreeOfParallelism = 10 });

        //    foreach (var x in lstStr)
        //    {
        //        await actionBlock.SendAsync(x);
        //    }
        //    actionBlock.Complete();

        //    await actionBlock.Completion;
        //}
    }
}
