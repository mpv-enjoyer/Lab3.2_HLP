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

StreamReader streamOrders = new("Orders.txt");
do
{
    current_line = streamOrders.ReadLine();
    orders.Add(int.Parse(current_line));
} while (!streamOrders.EndOfStream);
streamOrders.Close();

Queue<int> order_queue = new();
int current_order_id = 0;
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
            Console.WriteLine()
        }
    }
    foreach (Cook cook in cooks) cook.Tick(out int a);

} while (orders.Count != 0);

public class Cook
{
    int RemainingBusyTime { get; set; } = 0;
    public int Experience { get; }
    int MaxTime { get; } = 40;
    int CurrentOrderID = -1;
    public Cook(int experience) => Experience = Math.Min(experience, 40);
    public int NewOrder(int order_id)
    {
        if (RemainingBusyTime != 0 || CurrentOrderID != -1) throw new Exception("This cook is not free.");
        RemainingBusyTime = Math.Max(MaxTime * Experience / 80, MaxTime / 2);
        CurrentOrderID = order_id;
        return Math.Max(MaxTime * Experience / 80, MaxTime / 2);
    }
    public bool Tick(out int order_id) //Возвращает, свободен ли сейчас
    {
        order_id = CurrentOrderID;
        if (RemainingBusyTime == 0) return true;
        RemainingBusyTime -= 1;
        return RemainingBusyTime == 0;
    }
    public bool isFree()
    {
        return CurrentOrderID == 0;
    }
    public void Free()
    {
        if (RemainingBusyTime != 0) throw new Exception("Cannot free this cook.");
        CurrentOrderID = -1;
    }
}
class Courier
{
    int WasBuzyFor { get; set; } = 0;
    public int Stamina { get; set; }
    int RemainingBusyTime { get; set; } = 0;
    public int Strength { get; }
    public int Capacity { get; }
    int MaxTime { get; } = 80;
    int[] CurrentOrderIDs;
    public Courier(int stamina, int strength, int capacity)
    {
        Capacity = capacity;
        CurrentOrderIDs = new int[capacity];
        Stamina = Math.Min(stamina, 40);
        Strength = Math.Min(strength, 40);
    }
    public int NewOrder(int[] order_ids)
    {
        CurrentOrderIDs = order_ids;
        RemainingBusyTime = Math.Max(MaxTime * (40 - Stamina / 2) * (40 - Strength), MaxTime / 2);
        Stamina = (Stamina == 0) ? 0 : Stamina -= 1;
        WasBuzyFor = RemainingBusyTime;
        return RemainingBusyTime;
    }
    public bool Tick(out int[] order_ids) //Возвращает, свободен ли сейчас
    {
        order_ids = CurrentOrderIDs;
        if (RemainingBusyTime == 0) return true;
        RemainingBusyTime -= 1;
        if (RemainingBusyTime == 0)
        {
            CurrentOrderIDs = Array.Empty<int>();
            return true;
        }
        return false;
    }
    public void Report()
    {
        Console.WriteLine()
    }
}
class Storage
{
    public int Capacity { get; }
    Queue<int> Orders { get; set; }
    public Storage(int capacity)
    {
        Capacity = Math.Min(capacity, 15);
        Orders = new Queue<int>(Capacity);
    }
    public bool NewOrder(int order_id)
    {
        if (Orders.Count == Capacity) return false;
        Orders.Enqueue(order_id);
        return true;
    }
    public bool IsEmpty()
    {
        return Orders.Count == 0;
    }
    public int Pick()
    {
        return Orders.Dequeue();
    }
}