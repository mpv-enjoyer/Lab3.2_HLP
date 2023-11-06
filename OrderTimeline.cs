using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

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