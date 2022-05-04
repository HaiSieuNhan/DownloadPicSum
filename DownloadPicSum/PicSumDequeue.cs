using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;
using SkiaSharp;
using System.Diagnostics;
using static SkiaSharp.SKBitmapResizeMethod;

namespace DownloadPicSum
{
    public class PicSumDequeue
    {
        private DateTime EndTime { get; set; }
        private DateTime StartTime { get; set; }
        private bool IsStop { get; set; }
        private DequeueWorkerConfig Config { get; set; }
        private SizeImgConfig ImgResizeConfig { get; set; }
        private ConcurrentQueue<byte[]> QueueResizeImg { get; set; }
        private ConcurrentQueue<SKData> QueueSaveImg { get; set; }

        public PicSumDequeue()
        {
            IsStop = false;
            QueueResizeImg = new ConcurrentQueue<byte[]>();
            QueueSaveImg = new ConcurrentQueue<SKData>();
            Config = new DequeueWorkerConfig { BatchSize = 100 };
            StartTime = DateTime.Now;
            EndTime = StartTime.AddMinutes(1);
            Console.WriteLine($"End Time :  {EndTime}");
            //_imgResizeConfig = new SizeImgConfig { Width = 500, Height = 500, PathSave = "C:\\Users\\daoha\\OneDrive\\Desktop\\DownloadPicSum\\DownloadPicSum\\Img\\" };
            ImgResizeConfig = new SizeImgConfig
            {
                Width = 200,
                Height = 300,
                PathSave = "D:\\Working\\DownloadPicSum\\DownloadPicSum\\Img\\"
            };

        }

        public async Task Start()
        {
            HandleDownload();
            ResizeImg();
            HandleSaveFile();
        }

        private Task HandleDownload()
        {
            var tasks = new List<Task<byte[]>>();
            return Task.FromResult(Task.Run(async () =>
             {
                 while (!IsStop)
                 {
                     for (var i = 0; i < Config.BatchSize; i++)
                     {
                         using var client = new WebClient();
                         var url = $"https://picsum.photos/{ImgResizeConfig.Width}/{ImgResizeConfig.Height}";

                         tasks.Add(client.DownloadDataTaskAsync(url));
                        //Console.WriteLine($"Download Img {i + 1} => Success");
                    }

                     if (tasks.Count == 0)
                     {
                         Console.WriteLine($"Can't download picture");
                         continue;
                     }

                     var lstTaskByte = await Task.WhenAll(tasks);

                     var now = DateTime.Now.Minute;
                     var end = EndTime.Minute;

                     if (now >= end)
                     {
                         IsStop = true;
                     }

                     for (int i = 0; i < lstTaskByte.Length; i++)
                     {
                         byte[] taskByte = lstTaskByte[i];
                         QueueResizeImg.Enqueue(taskByte);
                         Console.WriteLine($"Process Send Queue Download Img {i} in {lstTaskByte.Length}");
                     }
                 }
             }));
        }

        private Task ResizeImg()
        {
            return Task.FromResult(Task.Run(async () =>
            {
                var lstResizeImg = new List<byte[]>();
                var actionResizeImgBlock = new ActionBlock<byte[]>(async (data) =>
                {
                    var original = SkiaSharp.SKBitmap.Decode(data)
                        .Resize(
                            new SKImageInfo(ImgResizeConfig.ResizedWidth,
                                ImgResizeConfig.ResizedHeight),
                            Lanczos3);

                    var image = SKImage.FromBitmap(original).Encode(SKEncodedImageFormat.Png, 90);

                    QueueSaveImg.Enqueue(image);

                }, new ExecutionDataflowBlockOptions { MaxDegreeOfParallelism = Environment.ProcessorCount });

                while (!IsStop)
                {
                    for (var i = 0; i < QueueResizeImg.Count; i++)
                    {
                        if (QueueResizeImg.TryDequeue(out var itm))
                        {
                            lstResizeImg.Add(itm);
                        }
                        else
                        {
                            break;
                        }
                    }

                    if (lstResizeImg.Count == 0) continue;

                     //var result = HandleResizeImg(datas);

                     //Console.WriteLine(result.IsCompleted
                     //    ? $"**** All task HandleResizeImg start and finish: {result.IsCompleted}"
                     //    : $"**** Waiting... HandleResizeImg");

                     //var timer = new Stopwatch();
                     //timer.Start();
                     //Console.WriteLine($"***** Watch Start HandleResizeImg: {timer.Elapsed:m\\:ss\\.fff}");

                     var idxQueue = 0;
                    for (; idxQueue < lstResizeImg.Count; idxQueue++)
                    {
                        var resizeImg = lstResizeImg[idxQueue];
                        await actionResizeImgBlock.SendAsync(resizeImg);
                        Console.WriteLine($"Process Send Queue Resize Img {idxQueue} in {lstResizeImg.Count}");
                    }

                     //actionBlock.Complete();
                     //await actionBlock.Completion;
                     //Console.WriteLine($"***** Watch End HandleResizeImg: {timer.Elapsed:m\\:ss\\.fff}");
                     //timer.Stop();
                 }
            }));
        }

        private async Task<ParallelLoopResult> HandleResizeImgUsingParallelLoopResult(IReadOnlyCollection<QueueResizeImg> queueResizeImtData)
        {
            var timer = new Stopwatch();
            timer.Start();
            Console.WriteLine($"***** Watch Start HandleResizeImg: {timer.Elapsed:m\\:ss\\.fff}");

            var result = await Task.Run(() => Parallel.ForEach(
                queueResizeImtData,
                new ParallelOptions { MaxDegreeOfParallelism = Environment.ProcessorCount },
                s =>
                {
                    var original = SkiaSharp.SKBitmap.Decode(s.Data)
                        .Resize(
                            new SKImageInfo(ImgResizeConfig.ResizedWidth,
                                ImgResizeConfig.ResizedHeight),
                            Lanczos3);

                    var image = SKImage.FromBitmap(original).Encode(SKEncodedImageFormat.Png, 90);

                    QueueSaveImg.Enqueue(image);
                }));

            Console.WriteLine($"***** Watch End HandleResizeImg: {timer.Elapsed:m\\:ss\\.fff}");
            timer.Stop();
            return result;
        }

        private async Task HandleResizeImgUsingActionBlock(IEnumerable<byte[]> lstByteResizeImg)
        {
            //var timer = new Stopwatch();
            //timer.Start();
            //Console.WriteLine($"***** Watch Start HandleResizeImg: {timer.Elapsed:m\\:ss\\.fff}");

            var actionBlock = new ActionBlock<byte[]>((data) =>
           {
               var original = SkiaSharp.SKBitmap.Decode(data)
                   .Resize(
                       new SKImageInfo(ImgResizeConfig.ResizedWidth,
                           ImgResizeConfig.ResizedHeight),
                       Lanczos3);

               var image = SKImage.FromBitmap(original).Encode(SKEncodedImageFormat.Png, 90);

               QueueSaveImg.Enqueue(image);

           }, new ExecutionDataflowBlockOptions { MaxDegreeOfParallelism = Environment.ProcessorCount });

            foreach (var x in lstByteResizeImg)
            {
                await actionBlock.SendAsync(x);
            }

            actionBlock.Complete();
            await actionBlock.Completion;

            //Console.WriteLine($"***** Watch End HandleResizeImg: {timer.Elapsed:m\\:ss\\.fff}");
            //timer.Stop();
        }

        private Task HandleSaveFile()
        {
            var lstSkData = new List<SKData>();

            var actionSaveFileBlock = new ActionBlock<SKData>(async (input) =>
            {
                var pathSave = ImgResizeConfig.PathSave + Guid.NewGuid() + ".png";
                await using var stream = new FileStream(pathSave, FileMode.Create, FileAccess.Write);
                input.SaveTo(stream);
            }, new ExecutionDataflowBlockOptions { MaxDegreeOfParallelism = Environment.ProcessorCount });

            return Task.FromResult(Task.Run(async () =>
            {
                while (!IsStop)
                {
                    var i = 0;
                    for (; i < QueueSaveImg.Count; i++)
                    {
                        var item = QueueSaveImg.TryDequeue(out var data);

                        if (!item) break;
                        else lstSkData.Add(data);
                    }

                    if (lstSkData.Count == 0) continue;

                     // Parallel.ForEach(
                     //     lstSkData,
                     // new ParallelOptions { MaxDegreeOfParallelism = 10 },
                     // s =>
                     // {
                     //     var pathSave = _imgResizeConfig.PathSave + Guid.NewGuid() + ".png";
                     //     using var stream = new FileStream(pathSave, FileMode.Create, FileAccess.Write);
                     //     s.SaveTo(stream);
                     // });

                     for (int i1 = 0; i1 < lstSkData.Count; i1++)
                    {
                        SKData x = lstSkData[i1];
                        await actionSaveFileBlock.SendAsync(x);
                        Console.WriteLine($"Process Send Queue Save File {i1} in {lstSkData.Count}");
                    }
                     //actionBlock.Complete();
                     //await actionBlock.Completion;
                 }
            }));
        }
    }

    public class DequeueWorkerConfig
    {
        public int IntervalMiliseconds { get; set; } = 1000;
        public int BatchSize { get; set; } = 200;
    }

    public class SizeImgConfig
    {
        public int Width { get; set; } = 200;
        public int Height { get; set; } = 300;
        public int ResizedWidth { get; set; } = 100;
        public int ResizedHeight { get; set; } = 150;
        public string PathSave { get; set; }
    }

    public abstract class QueueResizeImg
    {
        protected QueueResizeImg(byte[] data)
        {
            Data = data;
        }

        public byte[] Data { get; set; }
        public string QueueName { get; set; }
    }
}