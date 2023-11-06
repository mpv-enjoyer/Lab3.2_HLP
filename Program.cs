using System.Collections.Generic;
using System.Diagnostics.Metrics;
using System.Runtime.ExceptionServices;
using System.Security.Cryptography.X509Certificates;
using System.Text.Json;

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

const int max_storage = 10;
//const int time_deliver = 200; для Semi-real example
const int time_deliver = 90;
int current_tick = 0;

bool must_continue;
int current_order_id = 1;

do
{
    bool logs_changed = false;

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
            if (!is_repeating)
            {
                couriers_wait_for_orders.Enqueue(order.GetCourier());
            }
            Console.WriteLine($"Order {order.id} delivered by Courier {order.GetCourier().id}. In time: {order.IsInTime()}");
            logs_changed = true;
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
            Console.WriteLine($"Order {order.id} taken by Courier {courier.id} from storage.");
            logs_changed = true;
        }
        steps = Math.Min(orders_wait_for_storage.Count, capacity_left);
        for (int j = 0; j < steps; j++)
        {
            Order order = orders_wait_for_storage.Dequeue();
            orders_delivering.Add(order);
            order.FreeCook();
            cooks_wait_for_order.Enqueue(order.GetCook());
            order.SetCourier(courier);
            Console.WriteLine($"Order {order.id} taken by Courier {courier.id} from Cook {order.GetCook().id}.");
            logs_changed = true;
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
            Console.WriteLine($"Order {order.id} from Cook {order.GetCook().id} now in storage.");
            logs_changed = true;
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
            Console.WriteLine($"Order {order.id} from Cook {order.GetCook().id} is ready and is now waiting for storage.");
            logs_changed = true;
            continue;
        }
        cooks_wait_for_order.Enqueue(order.GetCook());
        order.FreeCook();
        orders_wait_for_courier.Enqueue(order);
        Console.WriteLine($"Order {order.id} from Cook {order.GetCook().id} is ready and in storage.");
        logs_changed = true;
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
        Console.WriteLine($"Order {order.id} now has a Cook {order.GetCook().id}");
        logs_changed = true;
    }

    //Добавляем заказов в ожидающие, если так нужно по таймлайну из файла
    if (incoming_orders.Count != 0)
    {
        incoming_orders.Peek().Tick();
        while (incoming_orders.Count != 0 && incoming_orders.Peek().ticks == 0)
        {
            incoming_orders.Dequeue();
            orders_wait_for_cook.Enqueue(new Order(time_deliver, current_order_id));
            Console.WriteLine($"Order {current_order_id} has been placed in a queue for a cook.");
            logs_changed = true;
            current_order_id++;
        }
    }
    current_tick++;
    must_continue = orders_cooking.Count != 0 || orders_delivering.Count != 0 || orders_wait_for_cook.Count != 0
        || orders_wait_for_courier.Count != 0 || orders_wait_for_storage.Count != 0;

    if (!logs_changed) continue;
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
    Console.Write("_ ");
    for (int i = 0; i < orders_wait_for_courier.Count; i++)
    {
        Console.Write(".");
    }
    Console.Write(" ");
    for (int i = 0; i < couriers.Count; i++)
    {
        Console.Write($"{couriers[i].GetStatus()}[{couriers[i].GetCapacityLeft()}] ");
    }
    Console.WriteLine("|");
    Console.WriteLine("=====================================");
} while (must_continue);

float succeded_orders = 0;
foreach (var order in orders_finished)
{
    if (order.IsInTime())
    {
        order.GetCook().Succeed();
        order.GetCourier().Succeed();
        succeded_orders++;
    }
    else
    {
        order.GetCook().Fail();
        order.GetCourier().Fail();
    }
    if (order.IsInTime()) Console.Write("+");
    else Console.Write("-");
}

Console.WriteLine("");
//Если меньше 90% заказов были оплачены, ищем ошибки.
if (succeded_orders / orders_finished.Count < 0.9)
{
    bool advice_given = false;
    List<int> done = new();
    List<float> ratios = new();
    for (int i = 0; i < couriers.Count; i++) //Проверяем курьеров на работоспособность
    {
        done.Add(couriers[i].Delivered());
        ratios.Add(couriers[i].Ratio());
    }
    for (int i = 0; i < couriers.Count; i++)
    {
        if (done.Average() / 3 > done[i] && ratios.Average() / 3 > ratios[i])
        {
            Console.WriteLine($"Уволить курьера {i}");
            advice_given = true;
        }
    }
    if (advice_given) return;
    done.Clear();
    ratios.Clear();
    for (int i = 0; i < cooks.Count; i++) //Проверяем поваров на работоспособность
    {
        done.Add(cooks[i].Cooked());
        ratios.Add(cooks[i].Ratio());
    }
    for (int i = 0; i < cooks.Count; i++)
    {
        if (done.Average() / 3 > done[i] && ratios.Average() / 3 > ratios[i])
        {
            Console.WriteLine($"Уволить повара {i}");
            advice_given = true;
        }
    }
    if (advice_given) return;
    ratios.Clear();
    for (int i = 0; i < orders_finished.Count; i++)
    {
        ratios.Add(orders_finished[i].WaitForCookRatio());
    }
    if (ratios.Average() > 0.2)
    {
        Console.WriteLine("Нанять повара.");
    }
    ratios.Clear();
    for (int i = 0; i < orders_finished.Count; i++)
    {
        ratios.Add(orders_finished[i].WaitForStorageRatio());
    }
    if (ratios.Average() > 0.2)
    {
        Console.WriteLine("Увеличить пространство на складе.");
    }
    Console.WriteLine("Нанять курьера.");
}
else
{
    Console.WriteLine($"{succeded_orders / orders_finished.Count * 100}% заказов было доставлено вовремя");
    Console.WriteLine("Увеличить количество заказов.");
}