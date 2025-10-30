using BenchmarkDotNet.Attributes;
using DropBear.Codex.Core.Results.Base;
using DropBear.Codex.Core.Results.Errors;

namespace DropBear.Codex.Benchmarks;

/// <summary>
///     Benchmarks for Result pattern operations vs traditional exception handling.
///     Measures the performance impact of using railway-oriented programming.
/// </summary>
[MemoryDiagnoser]
[SimpleJob(iterationCount: 100)]
public class ResultPatternBenchmarks
{
    private const int IterationCount = 1000;

    #region Benchmark: Result Pattern vs Exceptions (Success Path)

    [Benchmark(Baseline = true, Description = "Exception-based (Happy Path)")]
    public int ExceptionBased_HappyPath()
    {
        var sum = 0;
        for (var i = 0; i < IterationCount; i++)
        {
            sum += DivideWithException(100, 2);
        }
        return sum;
    }

    [Benchmark(Description = "Result Pattern (Happy Path)")]
    public int ResultPattern_HappyPath()
    {
        var sum = 0;
        for (var i = 0; i < IterationCount; i++)
        {
            var result = DivideWithResult(100, 2);
            if (result.IsSuccess)
            {
                sum += result.Value;
            }
        }
        return sum;
    }

    #endregion

    #region Benchmark: Result Pattern vs Exceptions (Error Path)

    [Benchmark(Description = "Exception-based (Error Path)")]
    public int ExceptionBased_ErrorPath()
    {
        var sum = 0;
        for (var i = 0; i < IterationCount; i++)
        {
            try
            {
                sum += DivideWithException(100, 0);
            }
            catch (DivideByZeroException)
            {
                sum += -1;
            }
        }
        return sum;
    }

    [Benchmark(Description = "Result Pattern (Error Path)")]
    public int ResultPattern_ErrorPath()
    {
        var sum = 0;
        for (var i = 0; i < IterationCount; i++)
        {
            var result = DivideWithResult(100, 0);
            sum += result.IsSuccess ? result.Value : -1;
        }
        return sum;
    }

    #endregion

    #region Benchmark: Chaining Operations

    [Benchmark(Description = "Result Chain: Multiple Operations")]
    public int ResultPattern_Chaining()
    {
        var sum = 0;
        for (var i = 0; i < IterationCount; i++)
        {
            var result1 = DivideWithResult(100, 2);
            if (!result1.IsSuccess)
            {
                sum += -1;
                continue;
            }

            var step2Value = result1.Value * 2;
            var result2 = DivideWithResult(step2Value, 4);

            if (result2.IsSuccess)
            {
                sum += result2.Value;
            }
            else
            {
                sum += -1;
            }
        }
        return sum;
    }

    [Benchmark(Description = "Manual Chaining (No Result)")]
    public int Manual_Chaining()
    {
        var sum = 0;
        for (var i = 0; i < IterationCount; i++)
        {
            try
            {
                var step1 = DivideWithException(100, 2);
                var step2 = step1 * 2;
                var step3 = DivideWithException(step2, 4);
                sum += step3;
            }
            catch (DivideByZeroException)
            {
                sum += -1;
            }
        }
        return sum;
    }

    #endregion

    #region Helper Methods

    private static int DivideWithException(int numerator, int denominator)
    {
        if (denominator == 0)
        {
            throw new DivideByZeroException();
        }
        return numerator / denominator;
    }

    private static Result<int, SimpleError> DivideWithResult(int numerator, int denominator)
    {
        if (denominator == 0)
        {
            return Result<int, SimpleError>.Failure(new SimpleError("Division by zero"));
        }
        return Result<int, SimpleError>.Success(numerator / denominator);
    }

    #endregion
}
