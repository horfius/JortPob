using JortPob.Common;
using JortPob.Model;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.IO;

namespace JortPob.Worker
{
    public class HkxWorker
    {
        public static void Go(List<CollisionInfo> collisions)
        {
            Lort.Log($"Converting {collisions.Count} collision...", Lort.Type.Main);                 // Egregiously slow, multithreaded to make less terrible
            Lort.NewTask("Converting HKX", collisions.Count);

            Parallel.ForEach(Partitioner.Create(0, collisions.Count), range =>
            {
                ProcessCollisions(collisions, range.Item1, range.Item2);
            });
        }

        protected static void ProcessCollisions(List<CollisionInfo> collisions, int start, int end)
        {
            int limit = Math.Min(collisions.Count, end);
            for (int i = start; i < limit; i++)
            {
                CollisionInfo collisionInfo = collisions[i];
                ModelConverter.OBJtoHKX(Path.Combine(Const.CACHE_PATH, collisionInfo.obj), Path.Combine(Const.CACHE_PATH, collisionInfo.hkx));
                Lort.TaskIterate(); // Progress bar update
            }
        }
    }
}
