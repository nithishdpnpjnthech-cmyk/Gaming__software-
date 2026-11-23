using System.Reflection;
using Xunit;

namespace GamingCafe.ClientAgent.Tests;

public static class Program
{
    public static async Task<int> Main(string[] args)
    {
        Console.WriteLine("==================================================");
        Console.WriteLine(" Gaming Cafe Client Agent - In-Process Test Runner");
        Console.WriteLine("==================================================");

        var testClasses = typeof(Program).Assembly.GetTypes()
            .Where(t => t.IsClass && !t.IsAbstract && t.Name.EndsWith("Tests"))
            .OrderBy(t => t.Name)
            .ToList();

        int totalPassed = 0;
        int totalFailed = 0;

        foreach (var testClass in testClasses)
        {
            Console.WriteLine($"\n[Running Suite] {testClass.Name}");
            var methods = testClass.GetMethods(BindingFlags.Public | BindingFlags.Instance);

            foreach (var method in methods)
            {
                var theoryAttr = method.GetCustomAttribute<TheoryAttribute>();
                var factAttr = method.GetCustomAttribute<FactAttribute>();

                if (theoryAttr != null)
                {
                    var inlineDataAttrs = method.GetCustomAttributes<InlineDataAttribute>().ToList();
                    foreach (var inlineData in inlineDataAttrs)
                    {
                        var data = inlineData.GetData(method).FirstOrDefault() ?? Array.Empty<object>();
                        var (passed, ex) = await RunTestMethodAsync(testClass, method, data);
                        var dataStr = string.Join(", ", data);
                        if (passed)
                        {
                            Console.WriteLine($"  [PASS] {method.Name}({dataStr})");
                            totalPassed++;
                        }
                        else
                        {
                            Console.ForegroundColor = ConsoleColor.Red;
                            Console.WriteLine($"  [FAIL] {method.Name}({dataStr}): {ex?.Message}");
                            Console.ResetColor();
                            totalFailed++;
                        }
                    }
                }
                else if (factAttr != null)
                {
                    var (passed, ex) = await RunTestMethodAsync(testClass, method, null);
                    if (passed)
                    {
                        Console.WriteLine($"  [PASS] {method.Name}");
                        totalPassed++;
                    }
                    else
                    {
                        Console.ForegroundColor = ConsoleColor.Red;
                        Console.WriteLine($"  [FAIL] {method.Name}: {ex?.Message}");
                        if (ex?.InnerException != null)
                        {
                            Console.WriteLine($"         Inner: {ex.InnerException.Message}");
                        }
                        Console.ResetColor();
                        totalFailed++;
                    }
                }
            }
        }

        Console.WriteLine("\n--------------------------------------------------");
        Console.WriteLine($"Test Execution Summary: Passed: {totalPassed}, Failed: {totalFailed}, Total: {totalPassed + totalFailed}");
        Console.WriteLine("--------------------------------------------------");

        return totalFailed == 0 ? 0 : 1;
    }

    private static async Task<(bool Passed, Exception? Ex)> RunTestMethodAsync(Type testClass, MethodInfo method, object[]? args)
    {
        object? instance = null;
        try
        {
            instance = Activator.CreateInstance(testClass);
            var result = method.Invoke(instance, args);
            if (result is Task task)
            {
                await task;
            }
            return (true, null);
        }
        catch (Exception ex)
        {
            var inner = ex is TargetInvocationException tie ? tie.InnerException : ex;
            return (false, inner);
        }
        finally
        {
            if (instance is IDisposable disposable)
            {
                disposable.Dispose();
            }
        }
    }
}
