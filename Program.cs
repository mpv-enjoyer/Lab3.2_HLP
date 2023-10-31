using System.Text.Json;

/* Предполагаем, что:
   Есть всего лишь 1 вид пиццы
   Есть только один дом для доставки

   Максимальный рабочий стаж, сила и выносливость - 40
   Максимальная вместимость очереди - 15 заказов
   Параметр выносливости падает в течение дня, восстанавливается на следующий день
   Всё время измеряется в минутах
   Длительность рабочего дня определяется входными данными.
   При поступлении нового заказа выбираем первого попавшегося повара / курьера

   Заказы тоже хранятся в файле: в виде INT_МИНУТ_БЕЗ_ЗАКАЗА\nINT_МИНУТ_БЕЗ_ЗАКАЗА ...*/

List<Courier> couriers = new();
List<Cook> cooks = new();

string current_line;
StreamReader streamCouriers = new("Couriers.json");
StreamReader streamCooks = new("Cooks.json");
StreamReader streamStorage = new("Storage.json");
do
{
    current_line = streamCouriers.ReadLine();
    couriers.Add(JsonSerializer.Deserialize<Courier>(current_line));
} while (!streamCouriers.EndOfStream);
do
{
    current_line = streamCooks.ReadLine();
    cooks.Add(JsonSerializer.Deserialize<Cook>(current_line));
} while (!streamCooks.EndOfStream);
Storage storage = JsonSerializer.Deserialize<Storage>(streamStorage.ReadLine());
streamCouriers.Close();
streamCooks.Close();
streamStorage.Close();

List<int> orders = new();

/*StreamReader streamOrders = new("Orders.txt");
do
{
    current_line = streamOrders.ReadLine();
    orders.Add(int.Parse(current_line));
} while (!streamOrders.EndOfStream);
streamOrders.Close();*/

Queue<int> order_queue = new();
int current_order_id = 0;

Cook testcook1 = new(30);
Order testorder1 = new();
Console.WriteLine(testcook1.isFree());
testorder1.SetCook(testcook1);
Console.WriteLine(testcook1.isFree());
return 0;

do
{
    //Начало тика. Проверяем, есть ли новый заказ.
    bool new_order = orders[0] == 0;
    if (new_order) orders.RemoveAt(0); else orders[0] -= 1;
    if (new_order)
    {
        order_queue.Enqueue(current_order_id);
        current_order_id++;
    }
    foreach (Courier courier in couriers)
    {
        int[] order_ids;
        if (courier.Tick(out order_ids) && order_ids.Count() != 0)
        {
            Console.WriteLine();
        }
    }
    foreach (Cook cook in cooks) cook.Tick(out int a);

} while (orders.Count != 0);

public enum CookStatus
{
    Free = 0,
    InProcess = 1,
    Waiting = 2
}

public class Cook
{
    CookStatus Status = CookStatus.Free;
    int RemainingBusyTime { get; set; } = 0;
    public int Experience { get; }
    int MaxTime { get; } = 40;
    public Cook(int experience) => Experience = Math.Min(experience, 40);
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
    }
}

public enum CourierStatus
{
    Free = 0,
    InProcess = 1,
}

class Courier
{
    public int Stamina { get; set; }
    int RemainingBusyTime { get; set; } = 0;
    public int Strength { get; }
    public int Capacity { get; }
    int MaxTime { get; } = 80;
    CourierStatus status = CourierStatus.Free;
    public Courier(int stamina, int strength, int capacity)
    {
        Capacity = capacity;
        Stamina = Math.Min(stamina, 40);
        Strength = Math.Min(strength, 40);
    }
    public int NewOrder()
    {
        RemainingBusyTime = Math.Max(MaxTime * (40 - Stamina / 2) * (40 - Strength), MaxTime / 2);
        Stamina = (Stamina == 0) ? 0 : Stamina -= 1;
        status = CourierStatus.InProcess;
        return RemainingBusyTime;
    }
    public void Tick()
    {
        if (status == CourierStatus.Free) return;
        RemainingBusyTime -= 1;
        if (RemainingBusyTime == 0) status = CourierStatus.Free;
    }
}
class Storage
{
    public int Capacity { get; }
    Queue<Order> Orders { get; set; }
    public Storage(int capacity)
    {
        Capacity = Math.Min(capacity, 15);
        Orders = new Queue<Order>(Capacity);
    }
    public bool NewOrder(Order new_order)
    {
        if (Orders.Count == Capacity) return false;
        Orders.Enqueue(new_order);
        return true;
    }
    public bool IsEmpty()
    {
        return Orders.Count == 0;
    }
    public Order Pick()
    {
        return Orders.Dequeue();
    }
}

public enum OrderStatus
{
    NoAvailableCook = 0,
    Cooking = 1,
    NoAvailableStorage = 2,
    Storing = 3,
    Delivering = 4,
    Done = 5 //unused?
}

class Order
{
    OrderStatus Stage = 0;
    int TicksOnThisStage = 0;
    Cook? CurrentCook;
    Courier? CurrentCourier;
    Storage CommonStorage;
    public Order(Storage storage) 
    {
        CommonStorage = storage;
    }
    public OrderStatus GetStage() { return Stage; }
    public int GetTicks() { return TicksOnThisStage; }
    public void SetCook(Cook new_cook)
    {
        CurrentCook = new_cook;
    }
    public void SetCourier(Courier new_courier)
    {
        CurrentCourier = new_courier;
    }
    public void UpdateOrder()
    {
        if (Stage == OrderStatus.Cooking)
        {
            CurrentCook.Tick();
            if (CurrentCook.GetStatus() == CookStatus.Waiting)
            {
                if (CommonStorage.NewOrder())
                Stage = OrderStatus.NoAvailableStorage;
            }
        }
        if (Stage == OrderStatus.Delivering) CurrentCourier.Tick();
    }
}