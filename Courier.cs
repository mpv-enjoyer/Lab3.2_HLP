using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

public enum CourierStatus
{
    Free = 0,
    InProcess = 1,
}
class Courier
{
    int succeded = 0;
    int failed = 0;
    public int id { get; }
    public int Stamina { get; set; }
    int RemainingBusyTime { get; set; } = 0;
    public int Strength { get; }
    public int Capacity { get; }
    int capacity_left;
    int MaxTime { get; } = 80;
    CourierStatus status = CourierStatus.Free;
    public Courier(int stamina, int strength, int capacity, int ID)
    {
        id = ID;
        Capacity = capacity;
        capacity_left = capacity;
        Stamina = Math.Min(stamina, 40);
        Strength = Math.Min(strength, 40);
    }
    public int NewOrder()
    {
        RemainingBusyTime = Math.Max(MaxTime * (40 - Stamina / 3) * (40 - Strength / 3) / 1600, MaxTime / 2);
        Stamina = (Stamina == 0) ? 0 : Stamina -= 1;
        status = CourierStatus.InProcess;
        if (capacity_left <= 0) throw new Exception("not enough capacity");
        capacity_left--;
        return RemainingBusyTime;
    }
    public void Tick()
    {
        if (status == CourierStatus.Free) return;
        RemainingBusyTime -= 1;
        if (RemainingBusyTime == 0)
        {
            capacity_left = Capacity;
            status = CourierStatus.Free;
        }
    }
    public CourierStatus GetStatus()
    {
        return status;
    }
    public int GetCapacityLeft()
    {
        return capacity_left;
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
    public int Delivered()
    {
        return succeded + failed;
    }
}
