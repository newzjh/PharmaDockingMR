using UnityEngine;
using System;
using System.Threading;
#if NETFX_CORE
using System.Threading.Tasks;
using Windows.System.Threading;
#endif
using System.IO;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices;


public class ThreadTask {

#if NETFX_CORE
    private Task task = null;
#else
    private Thread task = null;
#endif

    private bool running = false;
    private Delegate del;
    private object[] args;

    private object finishguard = new object();

    public ThreadTask(Delegate _del,object[] _args)
    {
        del = _del;
        args = _args;

        lock (finishguard)
        {
            running = true;
        }

#if NETFX_CORE 
        task = new Task(Work);
        task.Start();
#else
        task = new Thread(new ThreadStart(Work));
        task.Start();
#endif
    }


    private void Work()
    {
        if (del != null && args != null)
        {
            try
            {
                del.DynamicInvoke(args);
            }
            catch (Exception e)
            {
                Debug.LogError(e);
            }
        }
        lock (finishguard)
        {
            running = false;
        }
    }

    public bool IsCompleted
    {
        get 
        {
            bool ret;
            lock (finishguard)
            {
                ret = !running;
            }
            return ret; 
        }
    }

}


