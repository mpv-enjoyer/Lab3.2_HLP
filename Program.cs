using System.Collections.Generic;
using System.Runtime.ExceptionServices;
using System.Security.Cryptography.X509Certificates;
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
streamCouriers.Close();
streamCooks.Close();

Queue<OrderTimeline> incoming_orders = new();
StreamReader streamOrders = new("Orders.txt");
do
{
    current_line = streamOrders.ReadLine();
    if (!int.TryParse(current_line, out int f)) continue;
    incoming_orders.Enqueue(new OrderTimeline(int.Parse(current_line)));
} while (!streamOrders.EndOfStream);
streamOrders.Close();

Queue<Order> orders_wait_for_cook = new();
Queue<Cook> cooks_wait_for_order = new();
List<Order> orders_cooking = new();
Queue<Order> orders_wait_for_storage = new();
Queue<Order> orders_wait_for_courier = new();
Queue<Courier> couriers_wait_for_orders = new();
List<Order> orders_delivering = new();
List<Order> orders_finished = new();

foreach (var cook in cooks)
{
    cooks_wait_for_order.Enqueue(cook);
}
foreach (var courier in couriers)
{
    couriers_wait_for_orders.Enqueue(courier);
}

const int max_storage = 15;
const int time_deliver = 60;
int current_tick = 0;

bool must_continue;

do
{
    //Тик для каждого работника
    foreach (var cook in cooks) cook.Tick();
    foreach (var courier in couriers) courier.Tick();

    //Начинаем сверху, освобождаем курьеров
    List<Order> orders_transitioned_from_delivering = new();
    foreach (var order in orders_delivering)
    {
        if (order.Tick())
        {
            orders_finished.Add(order);
            bool is_repeating = false;
            foreach (var ord in orders_transitioned_from_delivering)
            {
                if (ord.GetCourier() == order.GetCourier()) is_repeating = true;
            }
            if (!is_repeating) couriers_wait_for_orders.Enqueue(order.GetCourier());
            orders_transitioned_from_delivering.Add(order);
        }
    }
    foreach (var order in orders_transitioned_from_delivering)
    {
        orders_delivering.Remove(order);
    }

    //Занимаем курьеров заказами со склада и заказами, ожидающими места в складе
    foreach (var order in orders_wait_for_courier)
    {
        order.Tick();
    }
    for (int i = 0; i < couriers_wait_for_orders.Count; i++)
    {
        if (orders_wait_for_courier.Count == 0 && orders_wait_for_storage.Count == 0) break;
        Courier courier = couriers_wait_for_orders.Dequeue();
        int capacity_left = courier.Capacity - Math.Min(orders_wait_for_courier.Count, courier.Capacity);
        int steps = Math.Min(orders_wait_for_courier.Count, courier.Capacity);
        for (int j = 0; j < steps; j++)
        {
            Order order = orders_wait_for_courier.Dequeue();
            orders_delivering.Add(order);
            order.SetCourier(courier);
        }
        steps = Math.Min(orders_wait_for_storage.Count, capacity_left);
        for (int j = 0; j < steps; j++)
        {
            Order order = orders_wait_for_storage.Dequeue();
            orders_delivering.Add(order);
            order.FreeCook();
            cooks_wait_for_order.Enqueue(order.GetCook());
            order.SetCourier(courier);
        }
    }

    //Оставшиеся заказы перемещаются в склад, если в нём освободилось место
    int orders_wait_storage_count = orders_wait_for_storage.Count;
    for (int i = 0; i < orders_wait_storage_count; i++)
    {
        if (orders_wait_for_courier.Count < max_storage)
        {
            var order = orders_wait_for_storage.Dequeue();
            cooks_wait_for_order.Enqueue(order.GetCook());
            order.FreeCook();
            orders_wait_for_courier.Enqueue(order);
            order.Tick();
        }
        else break;
    }

    //Приготовленные заказы попытаемся положить на склад, но если не получится,
    //повар будет ждать места, с заказом в orders_wait_for_courier.
    //Заказы, находящиеся в процессе готовки, не изменяем.
    List<Order> orders_transitioned_from_cooking = new();
    foreach (var order in orders_cooking)
    {
        bool cooked = order.Tick();
        if (!cooked) continue;
        orders_transitioned_from_cooking.Add(order);
        if (orders_wait_for_courier.Count == max_storage)
        {
            orders_wait_for_storage.Enqueue(order);
            continue;
        }
        cooks_wait_for_order.Enqueue(order.GetCook());
        order.FreeCook();
        orders_wait_for_courier.Enqueue(order);
    }
    foreach (var order in orders_transitioned_from_cooking)
    {
        orders_cooking.Remove(order);
    }

    //Ищем пары (свободный повар - ожидающий повара заказ)
    foreach (var order in orders_wait_for_cook)
    {
        order.Tick();
    }
    int temp_steps = Math.Min(orders_wait_for_cook.Count, cooks_wait_for_order.Count);
    for (int i = 0; i < temp_steps; i++)
    {
        var order = orders_wait_for_cook.Dequeue();
        var cook = cooks_wait_for_order.Dequeue();
        orders_cooking.Add(order);
        order.SetCook(cook);
    }

    //Добавляем заказов в ожидающие, если так нужно по таймлайну из файла
    if (incoming_orders.Count != 0)
    {
        incoming_orders.Peek().Tick();
        while (incoming_orders.Count != 0 && incoming_orders.Peek().ticks == 0)
        {
            incoming_orders.Dequeue();
            orders_wait_for_cook.Enqueue(new Order(time_deliver));
        }
    }
    current_tick++;
    must_continue = orders_cooking.Count != 0 || orders_delivering.Count != 0 || orders_wait_for_cook.Count != 0
        || orders_wait_for_courier.Count != 0 || orders_wait_for_storage.Count != 0;

    Console.Write($"{current_tick} ");
    for (int i = 0; i < orders_wait_for_cook.Count; i++)
    {
        Console.Write(".");
    }
    Console.Write(" ");
    for (int i = 0; i < cooks.Count; i++)
    {
        Console.Write($"{cooks[i].GetStatus()} ");
    }
    Console.Write(" _ ");
    for (int i = 0; i < orders_wait_for_courier.Count; i++)
    {
        Console.Write(".");
    }
    Console.Write(" ");
    for (int i = 0; i < couriers.Count; i++)
    {
        Console.Write($"{couriers[i].GetStatus()}[{couriers[i].GetCapacityLeft()}] ");
    }
    Console.WriteLine(";");
} while (must_continue);

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
        Status = CookStatus.Free;
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
    int capacity_left;
    int MaxTime { get; } = 80;
    CourierStatus status = CourierStatus.Free;
    public Courier(int stamina, int strength, int capacity)
    {
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
}

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
    OrderStatus Stage;
    Cook? CurrentCook;
    Courier? CurrentCourier;
    int ticks = 0;
    int ticks_awaited { get; }
    public Order(int ticks_remaining) { ticks_awaited = ticks_remaining; }
    public void SetCook(Cook new_cook)
    {
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
}

class OrderTimeline
{
    public int ticks { get; set; }
    public OrderTimeline(int ticks)
    {
        this.ticks = ticks;
    }
    public void Tick()
    {
        ticks--;
    }
}