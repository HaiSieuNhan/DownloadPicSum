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

        private bool IsStop { get; set; }
        private bool IsStopDownload { get; set; }
        private DequeueWorkerConfig Config { get; set; }
        private SizeImgConfig ImgResizeConfig { get; set; }
        private ConcurrentQueue<byte[]> QueueResizeImg { get; set; }
        private ConcurrentQueue<SKData> QueueSaveImg { get; set; }
        private ActionBlock<SKData> ActionSaveFileBlock { get; set; }
        private ActionBlock<byte[]> ActionResizeImgBlock { get; set; }
        private List<byte[]> LstResizeImg { get; set; }
        private List<SKData> LstSkData { get; set; }
        private List<Task<byte[]>> LstTasksByte { get; set; }

        public PicSumDequeue()
        {
            IsStop = false;
            IsStopDownload = false;
            LstResizeImg = new List<byte[]>();
            LstSkData = new List<SKData>();
            LstTasksByte = new List<Task<byte[]>>();
            QueueResizeImg = new ConcurrentQueue<byte[]>();
            QueueSaveImg = new ConcurrentQueue<SKData>();
            Config = new DequeueWorkerConfig(1) { };
            Console.WriteLine($"End Time :  {Config.EndTime}");
            //_imgResizeConfig = new SizeImgConfig { Width = 500, Height = 500, PathSave = "C:\\Users\\daoha\\OneDrive\\Desktop\\DownloadPicSum\\DownloadPicSum\\Img\\" };
            ImgResizeConfig = new SizeImgConfig
            {
                Width = 200,
                Height = 300,
                PathSave = "D:\\Working\\DownloadPicSum\\DownloadPicSum\\Img\\"
            };
            ActionSaveFileBlock = new ActionBlock<SKData>(async (input) =>
            {
                var pathSave = ImgResizeConfig.PathSave + Guid.NewGuid() + ".png";
                await using var stream = new FileStream(pathSave, FileMode.Create, FileAccess.Write);
                input.SaveTo(stream);
            }, new ExecutionDataflowBlockOptions { MaxDegreeOfParallelism = Environment.ProcessorCount });
            ActionResizeImgBlock = new ActionBlock<byte[]>(async (data) =>
            {
                var original = SkiaSharp.SKBitmap.Decode(data)
                    .Resize(
                        new SKImageInfo(ImgResizeConfig.ResizedWidth,
                            ImgResizeConfig.ResizedHeight),
                        Lanczos3);

                var image = SKImage.FromBitmap(original).Encode(SKEncodedImageFormat.Png, 90);

                QueueSaveImg.Enqueue(image);
            }, new ExecutionDataflowBlockOptions { MaxDegreeOfParallelism = Environment.ProcessorCount });
        }

        public Task Start()
        {
            var handleDownload = HandleDownload();
            var handleResize = ResizeImg();
            var handleSaveFile = HandleSaveFile();

            return Task.CompletedTask;
        }

        private Task HandleDownload()
        {
            return Task.FromResult(Task.Run(async () =>
            {
                while (!IsStopDownload)
                {
                    for (var i = 0; i < Config.BatchSize; i++)
                    {
                        using var client = new WebClient();
                        var url = $"https://picsum.photos/{ImgResizeConfig.Width}/{ImgResizeConfig.Height}";

                        LstTasksByte.Add(client.DownloadDataTaskAsync(url));
                        //Console.WriteLine($"Download Img {i + 1} => Success");
                    }

                    if (LstTasksByte.Count == 0)
                    {
                        Console.WriteLine($"Can't download picture");
                        continue;
                    }

                    var lstTaskByte = await Task.WhenAll(LstTasksByte);

                    if (DateTime.Now.Minute >= Config.EndTime.Minute)
                    {
                        IsStopDownload = true;
                        Console.WriteLine(
                            "=========================================== TIME END ===========================================");
                    }

                    for (var i = 0; i < lstTaskByte.Length; i++)
                    {
                        var taskByte = lstTaskByte[i];
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
                while (!IsStop)
                {
                    for (var i = 0; i < QueueResizeImg.Count; i++)
                    {
                        if (QueueResizeImg.TryDequeue(out var itm))
                        {
                            LstResizeImg.Add(itm);
                        }
                        else
                        {
                            break;
                        }
                    }

                    if (LstResizeImg.Count == 0)
                    {
                        Console.WriteLine("No Img To Resize");
                        await Task.Delay(1000);
                        continue;
                    }

                    //var result = HandleResizeImg(datas);

                    //Console.WriteLine(result.IsCompleted
                    //    ? $"**** All task HandleResizeImg start and finish: {result.IsCompleted}"
                    //    : $"**** Waiting... HandleResizeImg");

                    //var timer = new Stopwatch();
                    //timer.Start();
                    //Console.WriteLine($"***** Watch Start HandleResizeImg: {timer.Elapsed:m\\:ss\\.fff}");

                    var idxQueue = 0;
                    for (; idxQueue < LstResizeImg.Count; idxQueue++)
                    {
                        var resizeImg = LstResizeImg[idxQueue];
                        await ActionResizeImgBlock.SendAsync(resizeImg);
                        Console.WriteLine($"Process Send Queue Resize Img {idxQueue} in {LstResizeImg.Count}");
                    }

                    //actionBlock.Complete();
                    //await actionBlock.Completion;
                    //Console.WriteLine($"***** Watch End HandleResizeImg: {timer.Elapsed:m\\:ss\\.fff}");
                    //timer.Stop();
                }
            }));
        }

        private async Task<ParallelLoopResult> HandleResizeImgUsingParallelLoopResult(
            IReadOnlyCollection<QueueResizeImg> queueResizeImtData)
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
            return Task.FromResult(Task.Run(async () =>
            {
                while (!IsStop)
                {
                    var i = 0;
                    for (; i < QueueSaveImg.Count; i++)
                    {
                        var item = QueueSaveImg.TryDequeue(out var data);

                        if (!item) break;
                        else LstSkData.Add(data);
                    }

                    if (LstSkData.Count == 0)
                    {
                        Console.WriteLine("No File To Save");
                        await Task.Delay(1000);
                        continue;
                    }

                    // Parallel.ForEach(
                    //     lstSkData,
                    // new ParallelOptions { MaxDegreeOfParallelism = 10 },
                    // s =>
                    // {
                    //     var pathSave = _imgResizeConfig.PathSave + Guid.NewGuid() + ".png";
                    //     using var stream = new FileStream(pathSave, FileMode.Create, FileAccess.Write);
                    //     s.SaveTo(stream);
                    // });

                    for (var i1 = 0; i1 < LstSkData.Count; i1++)
                    {
                        var x = LstSkData[i1];
                        await ActionSaveFileBlock.SendAsync(x);
                        Console.WriteLine($"Process Send Queue Save File {i1} in {LstSkData.Count}");
                    }
                    //actionBlock.Complete();
                    //await actionBlock.Completion;
                }
            }));
        }
    }

    public class DequeueWorkerConfig
    {
        public DequeueWorkerConfig(int endTimeMinute, int batchSize = 200, int intervalMilisecond = 1000)
        {
            var now = DateTime.Now;
            StartTime = now;
            EndTime = now.AddMinutes(endTimeMinute);
            BatchSize = batchSize;
            IntervalMiliseconds = intervalMilisecond;
        }
        public int IntervalMiliseconds { get; set; }

        public int EndTimeMinute { get; set; }
        public DateTime EndTime { get; set; }
        public DateTime StartTime { get; set; }
        public int BatchSize { get; set; }
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