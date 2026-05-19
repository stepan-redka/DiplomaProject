using System;
class Program {
    static void Main() {
        try {
            Guid g = Guid.Parse("");
            Console.WriteLine($"Parsed: {g}");
        } catch (Exception ex) {
            Console.WriteLine($"Exception: {ex.GetType().Name}");
        }
    }
}
