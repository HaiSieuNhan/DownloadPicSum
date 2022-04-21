using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Drawing;

namespace DownloadPicSum
{
    public class PicSumDequeue
    {
        private bool _isStop;
        Task _taskDownloadImg;
        Task _taskResizeImg;
        Task _taskSaveImg;
        DequeueWorkerConfig _config;
        CancellationTokenSource _cancel;
        private const string _queueNameResize = "ResizeIMG";
        ConcurrentQueue<QueueResizeImg> _queueSplitedResizeImg = new ConcurrentQueue<QueueResizeImg>();
        public PicSumDequeue()
        {
            _isStop = false;
            _config = new DequeueWorkerConfig { };
        }
        public async Task Start()
        {
            //if (!_isStop)
            //{
            //    _cancel = new CancellationTokenSource();
            //}
            //CancellationToken cancellationToken = _cancel.Token;

            _taskDownloadImg = Task.Run(async () =>
            {
                var interval = _config.IntervalMiliseconds / 3;
                if (interval == 0) interval = 100;

                while (!_isStop)
                {
                    List<byte[]> items = new List<byte[]>();
                    try
                    {
                        //cancellationToken.ThrowIfCancellationRequested();

                        for (var i = 0; i < _config.BatchSize; i++)
                        {
                            using (WebClient client = new WebClient())
                            {
                                var url = "https://picsum.photos/200/300";

                                var itm = await client.DownloadDataTaskAsync(url);

                                if (itm.Length <= 0)
                                {
                                    break;
                                }
                                else
                                {
                                    items.Add(itm);
                                }
                            }
                        }

                        if (items.Count == 0)
                        {
                            Console.WriteLine($"{DateTime.Now} WARNING, No item in queue");
                            continue;
                        }

                        foreach (var i in items)
                        {
                            _queueSplitedResizeImg.Enqueue(new QueueResizeImg()
                            {
                                QueueName = _queueNameResize,
                                Data = i,
                            });
                        }

                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine(ex);
                    }
                    finally
                    {
                        await Task.Delay(interval);
                    }
                }
            });
        }

        private async Task<Task> ResizeIMG()
        {
            return Task.Run(async () =>
            {
                while (!_isStop)
                {
                    try
                    {
                        List<QueueResizeImg> datas = new List<QueueResizeImg>();

                        for (var i = 0; i < _config.BatchSize; i++)
                        {
                            if (_queueSplitedResizeImg.TryDequeue(out QueueResizeImg itm))
                            {
                                datas.Add(itm);
                            }
                            else
                            {
                                break;
                            }
                        }

                        if (datas.Count == 0) continue;

                        foreach (var item in datas)
                        {
                            HandleResizeImg(item);
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"{DateTime.Now} ERROR , {ex}");

                    }
                    finally
                    {
                        await Task.Delay(_config.IntervalMiliseconds);
                    }
                }
            });
        }

        private async Task HandleResizeImg(QueueResizeImg item)
        {
            var mstream = new MemoryStream();
            mstream.Write(item.Data, 0, item.Data.Length);
            mstream.Position = 0;
        }
    }

    public class DequeueWorkerConfig
    {
        public int IntervalMiliseconds { get; set; } = 1000;

        public int BatchSize { get; set; } = 2;

        public List<int> NotAllowHour { get; set; }
    }

    public class QueueResizeImg
    {
        public byte[] Data { get; set; }
        public string QueueName { get; set; }
    }
}
