using System;
using System.Threading.Tasks;
using UnityEngine;
using System.Threading;

public class WaitForThreadedTask : CustomYieldInstruction
{
    private volatile bool isDone = false;
    private readonly CancellationTokenSource cancellation = new CancellationTokenSource();
    
    public WaitForThreadedTask(Action action)
    {
        Task.Run(() => {
            try 
            {
                if (!cancellation.Token.IsCancellationRequested)
                {
                    action();
                    isDone = true;
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"Thread task failed: {e}");
                isDone = true;
            }
        }, cancellation.Token);
    }

    public override bool keepWaiting => !isDone;

    public void Cancel()
    {
        cancellation.Cancel();
        isDone = true;
    }

    ~WaitForThreadedTask()
    {
        cancellation.Cancel();
    }
} 