using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
public enum OrderStatus
{
    NoAvailableCook = 0,
    Cooking = 1,
    NoAvailableStorage = 2,
    Storing = 3,
    Delivering = 4,
    Done = 5
}
class Order
{
    public int id { get; }
    OrderStatus Stage;
    Cook? CurrentCook;
    Courier? CurrentCourier;
    int ticks = 0;
    int ticks_awaited { get; }
    int ticks_waiting_for_a_cook = 0;
    int ticks_waiting_for_storage = 0;
    public Order(int ticks_remaining, int ID)
    {
        ticks_awaited = ticks_remaining;
        id = ID;
    }
    public void SetCook(Cook new_cook)
    {
        ticks_waiting_for_a_cook = ticks;
        CurrentCook = new_cook;
        Stage = OrderStatus.Cooking;
        CurrentCook.NewOrder();
    }
    public void FreeCook()
    {
        CurrentCook.Free();
    }
    public void SetCourier(Courier new_courier)
    {
        if (CurrentCook == null) throw new Exception("Setting courier before cook was set");
        CurrentCourier = new_courier;
        Stage = OrderStatus.Delivering;
        CurrentCourier.NewOrder();
    }
    public Cook GetCook()
    {
        return CurrentCook;
    }
    public Courier GetCourier()
    {
        return CurrentCourier;
    }
    public bool Tick()
    {
        ticks++;
        switch (Stage)
        {
            case OrderStatus.NoAvailableStorage:
                ticks_waiting_for_storage++;
                break;
            case OrderStatus.Cooking:
                if (CurrentCook.GetStatus() == CookStatus.Waiting)
                {
                    Stage = OrderStatus.NoAvailableStorage;
                    return true;
                }
                break;
            case OrderStatus.Delivering:
                if (CurrentCourier.GetStatus() == CourierStatus.Free)
                {
                    Stage = OrderStatus.Done;
                    return true;
                }
                break;
        }
        return false;
    }
    public bool IsInTime()
    {
        if (CurrentCook == null || CurrentCourier == null || Stage != OrderStatus.Done) throw new Exception("This order wasn't finished");
        return ticks <= ticks_awaited;
    }
    public float WaitForCookRatio()
    {
        return (float)ticks_waiting_for_a_cook / ticks;
    }
    public float WaitForStorageRatio()
    {
        return (float)ticks_waiting_for_storage / ticks;
    }
}

