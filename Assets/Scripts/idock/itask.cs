using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
#if NETFX_CORE
using System.Threading.Tasks;
using Windows.System.Threading;
#endif

namespace idock
{
    public class AsynPool<T>
    {
        public delegate void TraceHandler(string msg, float progress);
        public static TraceHandler trace = null;

        private Dictionary<Delegate, ThreadTask> ars = new Dictionary<Delegate, ThreadTask>();

        //! Initializes the counter to 0 and its expected hit value to z.
        public void init(T z)
        {
            ars.Clear();
        }

        //! Increments the counter by 1 in a thread safe manner, and wakes up the calling thread waiting on the internal mutex.
        public void post(Delegate del, ThreadTask ar)
        {
            ars[del] = ar;
        }

        //! Waits until the counter reaches its expected hit value.
        public void wait()
        {
            int totalcount = ars.Count;

            while (ars.Count > 0)
            {
#if NETFX_CORE 
                Task.Delay(1000).Wait();
#else
                Thread.Sleep(1000);
#endif

                Delegate[] trains = new Delegate[ars.Count];
                ars.Keys.CopyTo(trains, 0);
                foreach (Delegate train in trains)
                {
                    ThreadTask ar = ars[train];
                    if (ar.IsCompleted)
                    {
                        ars.Remove(train);
                    }
                }


                int restnumber = totalcount - ars.Count;
#if UNITY_EDITOR_WIN || UNITY_STANDALONE_WIN
                string methodstr = trains[0].Method.ToString();
#else
                string methodstr = trains[0].ToString();
#endif
                int sublen=methodstr.IndexOf("(");
                if (sublen>0)
                    methodstr = methodstr.Substring(0, sublen);
                string msg = restnumber.ToString() + "/" + totalcount.ToString() + ",Method:" + methodstr;
                float progress = (float)restnumber / totalcount;
                if (trace != null)
                {
                    trace(msg, progress);
                }

            }
        }


        

        //private:
        //    mutex m;
        //    condition_variable cv;
        //    T n; //!< Expected hit value.
        //    T i; //!< Counter value.
    };



//    public class AsynResult
//    { }

//    public class iThread
//    {
//#if NETFX_CORE 
//        private Task task = null;
//#else
//        private Thread task = null;
//#endif

//        private static Queue<Delegate> requestingqueue = new Queue<Delegate>();
//        private static object requestguard = new object();

//        private static Queue<AsynResult> finishqueue = new Queue<AsynResult>();
//        private static object finishguard = new object();

//        private bool run = false;

//        public void Start()
//        {
//#if NETFX_CORE 
//            task = new Task(WorkThread);
//            task.Start();
//#else
//            task = new Thread(new ThreadStart(Work));
//            task.Start();
//#endif
//        }

//        public void Stop()
//        {
//            run = false;
//#if NETFX_CORE 
//            task.Wait();
//#else
//            task.Join();
//#endif
//            task = null;
//        }


//        private void Work()
//        {
//            run = true;
//            while (run)
//            {
//                Action action;
//                Delegate del=null;

//                lock (requestguard)
//                {
//                    if (requestingqueue.Count > 0)
//                        del = requestingqueue.Dequeue();
//                }

//                AsynResult ret = null;
//                if (del != null)
//                {
//                    del();
//                    ret = new AsynResult();
//                }


//                lock (finishguard)
//                {
//                    if (ret!=null)
//                        finishqueue.Enqueue(ret);
//                }

//#if NETFX_CORE 
//                Task.Delay(300).Wait();
//#else
//                Thread.Sleep(300);
//#endif
//            }
//        }

//        int count = 0;
//        public void Push(Delegate del)
//        {
//            lock (requestguard)
//            {
//                requestingqueue.Enqueue(del);
//            }
//            count++;
//        }

//        public void Peek()
//        {
//            AsynResult ret = null;
//            lock (finishguard)
//            {
//                while (finishqueue.Count > 0)
//                {
//                    ret = finishqueue.Dequeue();
//                    count--;
//                }
//            }
//        }

//        public int GetRunningCount()
//        {
//            return count;
//        }
//    }

//    public class iThreadPool
//    {
//        private List<iThread> threads=new List<iThread>();

//        public iThreadPool(int n)
//        {
//            iThread thread = new iThread();
//            threads.Add(thread);
//            thread.Start();
//        }

//        public ~iThreadPool()
//        {
//            for (int i = 0; i < threads.Count; i++)
//            {
//                threads[i].Stop();
//                threads[i] = null;
//            }
//        }

//        int cur = 0;
//        public void AddTask(Delegate del)
//        {
//            threads[cur].Push(del);
//            cur = (cur + 1) % threads.Count;
//        }

//        public void wait()
//        {
//            while (true)
//            {
//                for (int i = 0; i < threads.Count; i++)
//                {
//                    threads[i].Peek();
//                }

//                int totalcount = 0;
//                for (int i = 0; i < threads.Count; i++)
//                {
//                    totalcount += threads[i].GetRunningCount();
//                }

//                if (totalcount > 0)
//                {
//#if NETFX_CORE 
//                    Task.Delay(300).Wait();
//#else
//                    Thread.Sleep(300);
//#endif
//                }
//                else
//                {
//                    break;
//                }
//            }
//        }

//    }


}
