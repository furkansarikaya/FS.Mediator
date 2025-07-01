using System.Diagnostics;
using FS.Mediator.Core;
using FS.Mediator.Models.Enums;
using FS.Mediator.Models.Options;
using Microsoft.Extensions.Logging;

namespace FS.Mediator.Behaviors;

/// <summary>
/// Pipeline behavior that implements intelligent retry logic for handling transient failures.
/// This behavior wraps request handlers with sophisticated retry mechanisms that can
/// significantly improve the resilience of your distributed system.
/// 
/// The retry logic follows these principles:
/// 1. Only retry exceptions that are likely to be transient
/// 2. Use exponential backoff to avoid overwhelming failing systems
/// 3. Respect total timeout limits to prevent hanging operations
/// 4. Provide detailed logging for debugging and monitoring
/// </summary>
/// <typeparam name="TRequest">The type of request being processed.</typeparam>
/// <typeparam name="TResponse">The type of response returned by the request.</typeparam>
public class RetryBehavior<TRequest, TResponse>(
    ILogger<RetryBehavior<TRequest, TResponse>> logger,
    RetryPolicyOptions options) : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    private readonly ILogger<RetryBehavior<TRequest, TResponse>> _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    private readonly RetryPolicyOptions _options = options ?? throw new ArgumentNullException(nameof(options));
    private readonly Random _random = new(); // Used for jitter calculation

    /// <inheritdoc />
    public async Task<TResponse> HandleAsync(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken = default)
    {
        var requestName = typeof(TRequest).Name;
        var totalStopwatch = Stopwatch.StartNew();
        Exception? lastException = null;

        // Track our retry journey for detailed logging and debugging
        for (var attempt = 0; attempt <= _options.MaxRetryAttempts; attempt++)
        {
            try
            {
                // First attempt is not technically a "retry", so we log it differently
                if (attempt == 0)
                {
                    _logger.LogDebug("Executing request {RequestName} (initial attempt)", requestName);
                }
                else
                {
                    _logger.LogInformation("Retrying request {RequestName} (attempt {Attempt}/{MaxAttempts})", 
                        requestName, attempt + 1, _options.MaxRetryAttempts + 1);
                }

                // Execute the actual request handler
                var result = await next(cancellationToken);
                
                // Success! If this was a retry, log the good news
                if (attempt > 0)
                {
                    _logger.LogInformation("Request {RequestName} succeeded after {Attempt} retries in {TotalTime}ms", 
                        requestName, attempt, totalStopwatch.ElapsedMilliseconds);
                }
                
                return result;
            }
            catch (Exception ex)
            {
                lastException = ex;
                
                // Check if we should even attempt to retry this type of exception
                if (!_options.ShouldRetryPredicate(ex))
                {
                    _logger.LogDebug("Request {RequestName} failed with non-retryable exception: {ExceptionType}", 
                        requestName, ex.GetType().Name);
                    throw; // Rethrow immediately for non-retryable exceptions
                }

                // Check if we've exceeded our total retry time budget
                if (totalStopwatch.Elapsed >= _options.MaxTotalRetryTime)
                {
                    _logger.LogWarning("Request {RequestName} exceeded maximum total retry time ({MaxTime}ms) after {Attempts} attempts", 
                        requestName, _options.MaxTotalRetryTime.TotalMilliseconds, attempt + 1);
                    throw; // We've spent too much time on this already
                }

                // If this was our last allowed attempt, don't bother calculating delay
                if (attempt >= _options.MaxRetryAttempts)
                {
                    _logger.LogWarning("Request {RequestName} failed after {MaxAttempts} attempts. Final exception: {Exception}", 
                        requestName, _options.MaxRetryAttempts + 1, ex.Message);
                    throw; // We've used up all our retry attempts
                }

                // Calculate how long to wait before the next attempt
                var delay = CalculateDelay(attempt);
                
                _logger.LogWarning("Request {RequestName} failed on attempt {Attempt} with {ExceptionType}: {Message}. Retrying in {Delay}ms", 
                    requestName, attempt + 1, ex.GetType().Name, ex.Message, delay.TotalMilliseconds);

                // Wait for the calculated delay (unless cancellation is requested)
                await Task.Delay(delay, cancellationToken);
            }
        }

        // If we reach here, we've exhausted all retries - throw the last exception we caught
        throw lastException ?? new InvalidOperationException("Retry loop completed without success or exception");
    }

    /// <summary>
    /// Calculates the delay before the next retry attempt based on the configured strategy.
    /// This method implements the mathematical logic behind different retry strategies,
    /// each optimized for different failure scenarios.
    /// </summary>
    /// <param name="attemptNumber">Zero-based attempt number (0 = first retry)</param>
    /// <returns>The calculated delay before the next attempt</returns>
    private TimeSpan CalculateDelay(int attemptNumber)
    {
        return _options.Strategy switch
        {
            RetryStrategy.FixedDelay => _options.InitialDelay,
            
            RetryStrategy.ExponentialBackoff => 
                TimeSpan.FromMilliseconds(_options.InitialDelay.TotalMilliseconds * Math.Pow(2, attemptNumber)),
            
            RetryStrategy.ExponentialBackoffWithJitter => 
                CalculateJitteredDelay(attemptNumber),
            
            _ => _options.InitialDelay
        };
    }

    /// <summary>
    /// Calculates exponential backoff delay with added jitter to prevent thundering herd problems.
    /// The jitter uses a ±25% random variation to spread out retry attempts across time.
    /// </summary>
    /// <param name="attemptNumber">Zero-based attempt number</param>
    /// <returns>Exponential delay with random jitter applied</returns>
    private TimeSpan CalculateJitteredDelay(int attemptNumber)
    {
        // Calculate base exponential delay
        var baseDelay = _options.InitialDelay.TotalMilliseconds * Math.Pow(2, attemptNumber);
        
        // Add jitter: ±25% random variation
        // This prevents all clients from retrying at exactly the same time
        var jitterRange = baseDelay * 0.25; // 25% of base delay
        var jitter = (_random.NextDouble() - 0.5) * 2 * jitterRange; // Random between -jitterRange and +jitterRange
        
        var finalDelay = Math.Max(0, baseDelay + jitter); // Ensure we never get negative delay
        
        return TimeSpan.FromMilliseconds(finalDelay);
    }
}
