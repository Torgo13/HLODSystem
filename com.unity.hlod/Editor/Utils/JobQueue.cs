
using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;

namespace Unity.HLODSystem.Utils
{
    public class JobQueue : IDisposable
    {        
        public JobQueue(int threadCount)
        {
            m_workers = new Worker[threadCount];
            for (int i = 0; i < threadCount; ++i)
            {
                m_workers[i] = new Worker(this);
            }
        }
        
        public void EnqueueMainThreadJob(Action job)
        {
            m_mainThreadJobs.Enqueue(job);
        }

        public void EnqueueJob(Action job)
        {
            m_jobs.Enqueue(job);
        }

        private Action? DequeueMainThreadJob()
        {
            return m_mainThreadJobs.TryDequeue(out Action? mainThreadJob) ? mainThreadJob : null;
        }
        private Action? DequeueJob()
        {
            return m_jobs.TryDequeue(out Action? job) ? job : null;
        }

        

        public IEnumerator WaitFinish()
        {
            bool isFinish = false;
            while (isFinish == false)
            {
                while (true)
                {
                    Action? mainThreadJob = DequeueMainThreadJob();
                    if (mainThreadJob == null)
                        break;
                    mainThreadJob.Invoke();
                }

                if (m_jobs.Count > 0)
                {
                    yield return null;
                    continue;
                }

                isFinish = true;
                for (int i = 0; i < m_workers.Length; ++i)
                {
                    if ( m_workers[i].IsException())
                    {
                        throw new Exception("Exception from worker thread.");
                    }
                    if (m_workers[i].IsWorking() == true)
                    {
                        isFinish = false;
                    }
                }

                if (isFinish == false)
                    yield return null;

            }
            
        }

        public void Dispose()
        {
            for ( int i = 0; i < m_workers.Length; ++i )
            {
                m_workers[i].Stop();
            }
#if OPTIMISATION_NULL
#else
            m_workers = null;
#endif // OPTIMISATION_NULL
        }

        private Worker[] m_workers;
        
        private readonly System.Collections.Concurrent.ConcurrentQueue<Action> m_mainThreadJobs
            = new System.Collections.Concurrent.ConcurrentQueue<Action>();
        private readonly System.Collections.Concurrent.ConcurrentQueue<Action> m_jobs
            = new System.Collections.Concurrent.ConcurrentQueue<Action>();
        
        #region worker

        class Worker
        {
            private JobQueue m_queue;
            
            private Thread m_thread;
            
            private bool m_terminated;
            private bool m_working;
            private bool m_exception;


            public Worker(JobQueue queue)
            {
                m_queue = queue;
                m_thread = new Thread(Run);
                m_thread.Start();
                
                m_terminated = false;
                m_working = false;
                m_exception = false;
            }

            public void Stop()
            {
                m_terminated = true;
            }

            public bool IsException()
            {
                return m_exception;
            }
            public bool IsWorking()
            {
                return m_working;
            }

            // ReSharper disable Unity.PerformanceAnalysis
            private void Run()
            {
                while (m_terminated == false)
                {
                    try
                    {
                        m_working = true;
                        Action? job = m_queue.DequeueJob();
                        if (job == null)
                        {
                            m_working = false;
                            Thread.Sleep(100);
                            continue;
                        }

                        job.Invoke();
                        m_working = false;
                    }
                    catch(Exception e)
                    {
                        Debug.LogException(e);
                        m_exception = true;
                    }
                    finally
                    {
                        m_working = false;
                    }
                }
            }
        }
        #endregion
    }
}