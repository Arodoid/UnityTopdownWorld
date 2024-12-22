using System;
using System.Threading.Tasks;
using UnityEngine;

public class WaitForThreadedTask : CustomYieldInstruction
{
    private bool isDone = false;
    
    public WaitForThreadedTask(Action action)
    {
        Task.Run(() => {
            action();
            isDone = true;
        });
    }

    public override bool keepWaiting => !isDone;
} 