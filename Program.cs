using System.Text.Json;
/* Предполагаем, что:
   Есть всего лишь 1 вид пиццы
   Есть только один дом для доставки

   Максимальный рабочий стаж, сила и выносливость - 40
   Максимальная вместимость очереди - 15 заказов
   Параметр выносливости падает в течение дня, восстанавливается на следующий день
   Всё время измеряется в минутах
   Длительность рабочего дня - 10 часов => 600 минут
   При поступлении нового заказа выбираем первого попавшегося повара / курьера

   Заказы тоже хранятся в файле: в виде INT_МИНУТ_БЕЗ_ЗАКАЗА INT_МИНУТ_БЕЗ_ЗАКАЗА ...*/

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



public class Cook
{
    int RemainingBusyTime { get; set; } = 0;
    public int Experience { get; }
    int MaxTime { get; } = 40;
    int CurrentOrderID = -1;
    public Cook(int experience) => Experience = Math.Min(experience, 40);
    public int NewOrder(int order_id)
    {
        RemainingBusyTime = Math.Max(MaxTime * Experience / 80, MaxTime / 2);
        CurrentOrderID = order_id;
        return Math.Max(MaxTime * Experience / 80, MaxTime / 2);
    }
    public bool Tick(out int order_id) //Возвращает, свободен ли сейчас
    {
        order_id = CurrentOrderID;
        if (RemainingBusyTime == 0) return true;
        RemainingBusyTime -= 1;
        if (RemainingBusyTime == 0)
        {
            CurrentOrderID = -1;
            return true;
        }
        return false;
    }
}
class Courier
{
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
    public int PeekOrder()
    {
        return Orders.Dequeue();
    }
}