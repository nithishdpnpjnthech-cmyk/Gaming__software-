using System.Reflection;
using Xunit;

namespace GamingCafe.ClientAgent.Tests;

public class Program
{
    public static async Task<int> Main(string[] args)
    {
        Console.WriteLine("=========================================================");
        Console.WriteLine("   GamingCafe.ClientAgent Test Suite (In-Process Runner) ");
        Console.WriteLine("=========================================================");

        var testClasses = Assembly.GetExecutingAssembly().GetTypes()
            .Where(t => t.IsClass && !t.IsAbstract && t.Name.EndsWith("Tests"))
            .OrderBy(t => t.Name)
            .ToList();

        int passed = 0;
        int failed = 0;
        var failedTests = new List<(string Name, Exception Ex)>();

        foreach (var testClass in testClasses)
        {
            Console.WriteLine($"\n[RUNNING SUITE] {testClass.Name}");
            var methods = testClass.GetMethods(BindingFlags.Public | BindingFlags.Instance)
                .Where(m => m.GetCustomAttribute<FactAttribute>() != null)
                .OrderBy(m => m.Name);

            foreach (var method in methods)
            {
                var testName = $"{testClass.Name}.{method.Name}";
                try
                {
                    var instance = Activator.CreateInstance(testClass);
                    var result = method.Invoke(instance, null);

                    if (result is Task task)
                    {
                        await task;
                    }

                    Console.WriteLine($"  [PASS] {method.Name}");
                    passed++;
                }
                catch (Exception ex)
                {
                    var inner = ex is TargetInvocationException tie && tie.InnerException != null
                        ? tie.InnerException
                        : ex;

                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine($"  [FAIL] {method.Name} --> {inner.Message}");
                    Console.ResetColor();
                    failed++;
                    failedTests.Add((testName, inner));
                }
            }
        }

        Console.WriteLine("\n=========================================================");
        Console.WriteLine($"Summary: {passed + failed} Total | {passed} Passed | {failed} Failed");
        Console.WriteLine("=========================================================");

        if (failed > 0)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine("\nFailures:");
            foreach (var f in failedTests)
            {
                Console.WriteLine($"- {f.Name}: {f.Ex.Message}");
                Console.WriteLine(f.Ex.StackTrace);
            }
            Console.ResetColor();
            return 1;
        }

        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine("ALL TESTS PASSED SUCCESSFULLY!");
        Console.ResetColor();
        return 0;
    }
}
