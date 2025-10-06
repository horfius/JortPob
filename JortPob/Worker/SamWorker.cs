using JortPob.Common;
using SoulsFormats;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using static JortPob.Faction;

namespace JortPob.Worker
{
    public class SamWorker : Worker
    {
        private readonly List<SoundManager.SAMData> datas;
        private readonly int start;
        private readonly int end;

        public SamWorker(List<SoundManager.SAMData> datas, int start, int end)
        {
            this.datas = datas;
            this.start = start;
            this.end = end;

            _thread = new Thread(Run);
            _thread.Start();
        }

        private void Run()
        {
            ExitCode = 1;

            for(int i = start;i<Math.Min(datas.Count(), end);i++)
            {
                SoundManager.SAMData dat = datas[i];
                SAM.Generate(dat.dialog, dat.info, dat.line, dat.hashName, dat.npc);
                Lort.TaskIterate(); // Progress bar update
            }

            IsDone = true;
            ExitCode = 0;
        }

        public static void Go(List<SoundManager.SAMData> datas)
        {
            Lort.Log($"Generating {datas.Count()} WEMs...", Lort.Type.Main);
            Lort.NewTask("Writing WEMs", datas.Count);

            int partition = (int)Math.Ceiling(datas.Count / (float)Const.THREAD_COUNT);
            List<SamWorker> workers = new();

            for (int i = 0; i < Const.THREAD_COUNT; i++)
            {
                int start = i * partition;
                int end = start + partition;
                SamWorker worker = new(datas, start, end);
                workers.Add(worker);
            }

            /* Wait for threads to finish */
            while (true)
            {
                bool done = true;
                foreach (SamWorker worker in workers)
                {
                    done &= worker.IsDone;
                }

                if (done)
                    break;
                Thread.Yield();
            }
        }
    }
}
