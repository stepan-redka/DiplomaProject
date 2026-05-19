using System;
using Qdrant.Client.Grpc;

class Program {
    static void Main() {
        Guid g = Guid.NewGuid();
        PointId pid = g;
        Console.WriteLine($"Guid: {g}");
        Console.WriteLine($"PointId Uuid: {pid.Uuid}");
        Console.WriteLine($"PointId Num: {pid.Num}");
    }
}
