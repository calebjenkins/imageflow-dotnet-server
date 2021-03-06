// Copyright (c) Imazen LLC.
// No part of this project, including this file, may be copied, modified,
// propagated, or distributed except as permitted in COPYRIGHT.txt.
// Licensed under the GNU Affero General Public License, Version 3.0.
// Commercial licenses available at http://imageresizing.net/

using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Imazen.DiskCache {
    internal class AsyncWriteCollection {

        public AsyncWriteCollection() {
            MaxQueueBytes = 1024 * 1024 * 100;
        }

        private readonly object sync = new object();

        private readonly Dictionary<string, AsyncWrite> c = new Dictionary<string, AsyncWrite>();

        /// <summary>
        /// How many bytes of buffered file data to hold in memory before refusing further queue requests and forcing them to be executed synchronously.
        /// </summary>
        public long MaxQueueBytes { get; set; }

        /// <summary>
        /// If the collection contains the specified item, it is returned. Otherwise, null is returned.
        /// </summary>
        /// <param name="key"></param>
        /// <returns></returns>
        public AsyncWrite Get(string key) {
            lock (sync) {
                return c.TryGetValue(key, out var result) ? result : null;
            }
        }

        /// <summary>
        /// Returns how many bytes are allocated by buffers in the queue. May be 2x the amount of data. Represents how much ram is being used by the queue, not the amount of encoded bytes that will actually be written.
        /// </summary>
        /// <returns></returns>
        private long GetQueuedBufferBytes() {
            lock (sync) {
                long total = 0;
                foreach (AsyncWrite value in c.Values) {
                    if (value == null) continue;
                    total += value.GetBufferLength();
                }
                return total;
            }
        }

        /// <summary>
        /// Removes the specified object based on its relativePath and modifiedDateUtc values.
        /// </summary>
        /// <param name="w"></param>
        public void Remove(AsyncWrite w) {
            lock (sync) {
                c.Remove(w.Key);
            }
        }
        public enum AsyncQueueResult
        {
            Enqueued,
            AlreadyPresent,
            QueueFull
        }
        /// <summary>
        /// Tries to enqueue the given async write and callback
        /// </summary>
        /// <param name="w"></param>
        /// <param name="writerDelegate"></param>
        /// <returns></returns>
        public AsyncQueueResult Queue(AsyncWrite w, Func<AsyncWrite, Task> writerDelegate){
            lock (sync) {
                if (GetQueuedBufferBytes() + w.GetBufferLength() > MaxQueueBytes) return AsyncQueueResult.QueueFull; //Because we would use too much ram.
                if (c.ContainsKey(w.Key)) return AsyncQueueResult.AlreadyPresent; //We already have a queued write for this data.
                c.Add(w.Key, w);
                Task.Run(
                    async () => {
                        try
                        {
                            await writerDelegate(w);
                        }
                        finally
                        {
                            Remove(w);
                        }
                    }).ConfigureAwait(false);
                return AsyncQueueResult.Enqueued;
            }
        }
        
    }
}
