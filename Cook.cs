using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

public enum CookStatus
{
    Free = 0,
    InProcess = 1,
    Waiting = 2
}
class Cook
{
    int failed = 0;
    int succeded = 0;
    public int id { get; }
    CookStatus Status = CookStatus.Free;
    int RemainingBusyTime { get; set; } = 0;
    public int Experience { get; }
    int MaxTime { get; } = 40;
    public Cook(int experience, int ID)
    {
        id = ID;
        Experience = Math.Min(experience, 40);
    }
    public int NewOrder()
    {
        if (Status != CookStatus.Free) throw new Exception("This cook is not free.");
        RemainingBusyTime = Math.Max(MaxTime * Experience / 80, MaxTime / 2);
        Status = CookStatus.InProcess;
        return Math.Max(MaxTime * Experience / 80, MaxTime / 2);
    }
    public void Tick()
    {
        if (Status != CookStatus.InProcess) return;
        RemainingBusyTime -= 1;
        if (RemainingBusyTime == 0) Status = CookStatus.Waiting;
    }
    public CookStatus GetStatus()
    {
        return Status;
    }
    public void Free()
    {
        if (RemainingBusyTime != 0) throw new Exception("Cannot free this cook.");
        Status = CookStatus.Free;
    }
    public void Fail()
    {
        failed += 1;
    }
    public void Succeed()
    {
        succeded += 1;
    }
    public float Ratio()
    {
        return (float)succeded / failed;
    }
    public int Cooked()
    {
        return succeded + failed;
    }
}