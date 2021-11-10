using System;
using System.Configuration;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;

public class RetryHelper
{
    public static async Task RetryOnExceptionAsync<TException>(int maxAttempts, Func<Task> operation, CancellationToken token) where TException : Exception
    {
        if (maxAttempts <= 0)
            throw new ArgumentOutOfRangeException(nameof(maxAttempts));

        var attempts = 0;
        TException exception;
        do
        {
            try
            {
                DebugHelper.Default("Attempt #" + attempts);
                await operation();
                break;
            }
            catch (TException ex)
            {
                DebugHelper.Default("RetryHelper Exception encountered: " + ex.Message);

                if (attempts == maxAttempts)
                    throw;

                exception = ex;
            }

            attempts++;
            token.ThrowIfCancellationRequested();
            await CreateDelayForException(maxAttempts, attempts, exception);

        } while (true);
    }

    private static Task CreateDelayForException(int maxAttempts, int attempts, Exception ex = null)
    {
        var nextDelay = IncreasingDelayInSeconds(attempts - 1);
        DebugHelper.Default($"Exception on attempt {attempts} of {maxAttempts}. Will retry after sleeping for {nextDelay}. " + ex.Message);
        return Task.Delay(nextDelay);
    }

    static TimeSpan IncreasingDelayInSeconds(int failedAttempts)
    {
        if (failedAttempts < 0) throw new ArgumentOutOfRangeException();

        return failedAttempts > DelayPerAttemptInSeconds.Length ? DelayPerAttemptInSeconds.Last() : DelayPerAttemptInSeconds[failedAttempts];
    }

    internal static TimeSpan[] DelayPerAttemptInSeconds =
    {
      TimeSpan.FromSeconds(2),
      TimeSpan.FromSeconds(3),
      TimeSpan.FromSeconds(5)
   };


    async public static void CheckingGhostSession()
    {
        var attempts = 0;

        await Task.Delay(TimeSpan.FromSeconds(10));

        do
        {
            //if (_portal._guidToClientData.Count == 0)
            //{
            attempts++;
            //    DebugHelper.Default("Warning Ghost Session, attempt check : " + attempts);
            //}
            //else
            //    attempts = 0;            

            if (attempts > 5)
                break;

            await Task.Delay(TimeSpan.FromSeconds(5));

        } while (true);

        GameLiftHandlerService.HandleGameEnd();
    }
}
